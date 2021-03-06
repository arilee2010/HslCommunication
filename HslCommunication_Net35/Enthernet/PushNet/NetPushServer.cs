﻿using HslCommunication.Core.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using HslCommunication.Core;



/**********************************************************************************
 * 
 *    发布订阅类的服务器类
 *    
 *    实现从客户端进行数据的订阅操作
 * 
 *********************************************************************************/




namespace HslCommunication.Enthernet
{
    /// <summary>
    /// 发布订阅服务器的类，支持按照关键字进行数据信息的订阅
    /// </summary>
    public class NetPushServer : NetworkServerBase
    {
        #region Constructor

        /// <summary>
        /// 实例化一个对象
        /// </summary>
        public NetPushServer()
        {
            dictPushClients = new Dictionary<string, PushGroupClient>( );
            dicHybirdLock = new SimpleHybirdLock( );
            sendAction = new Action<AppSession, string>( SendString );
        }


        #endregion
        
        #region Server Override

        /// <summary>
        /// 处理请求接收连接后的方法
        /// </summary>
        /// <param name="obj"></param>
        protected override void ThreadPoolLogin( object obj )
        {
            if (obj is Socket socket)
            {
                // 接收一条信息，指定当前请求的数据订阅信息的关键字
                OperateResult<int, string> receive = ReceiveStringContentFromSocket( socket );
                if (!receive.IsSuccess) return;

                // 判断当前的关键字在服务器是否有消息发布
                if(IsPushGroupOnline(receive.Content2))
                {
                    LogNet?.WriteWarn( ToString( ), "当前订阅的关键字不存在" );
                    socket?.Close( );
                    return;
                }

                // 允许发布订阅信息
                AppSession session = new AppSession( );
                session.KeyGroup = receive.Content2;
                session.WorkSocket = socket;
                try
                {
                    session.IpEndPoint = (System.Net.IPEndPoint)socket.RemoteEndPoint;
                    session.IpAddress = session.IpEndPoint.Address.ToString( );
                }
                catch (Exception ex)
                {
                    LogNet?.WriteException( ToString( ), "Ip信息获取失败", ex );
                }

                try
                {
                    socket.BeginReceive( session.BytesHead, 0, session.BytesHead.Length, SocketFlags.None, new AsyncCallback( ReceiveCallback ), session );
                }
                catch(Exception ex)
                {
                    LogNet?.WriteException( ToString( ), "开启信息接收失败", ex );
                    return;
                }

                LogNet?.WriteDebug( ToString( ), $"客户端 [ {session.IpEndPoint} ] 上线" );

                GetPushGroupClient( receive.Content2 )?.AddPushClient( session );
                
            }
        }


        #endregion

        #region Public Method

        /// <summary>
        /// 主动推送数据内容
        /// </summary>
        /// <param name="key"></param>
        /// <param name="content"></param>
        public void PushString( string key, string content )
        {
            AddPushKey( key );
            GetPushGroupClient( key )?.PushString( content, sendAction );
        }

        #endregion

        #region Private Method


        private void ReceiveCallback( IAsyncResult ar )
        {
            if (ar.AsyncState is AppSession session)
            {
                try
                {
                    Socket client = session.WorkSocket;
                    int bytesRead = client.EndReceive( ar );

                    // 正常下线退出
                    LogNet?.WriteInfo( ToString( ), $"客户端 {session.IpEndPoint} 下线" );
                    RemoveGroupOnlien( session.KeyGroup, session.ClientUniqueID );
                }
                catch (Exception ex)
                {
                    LogNet?.WriteException( ToString( ), $"客户端 {session.IpEndPoint} 下线", ex );
                    RemoveGroupOnlien( session.KeyGroup, session.ClientUniqueID );
                }
            }
        }



        /// <summary>
        /// 判断当前的关键字订阅是否在服务器的词典里面
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private bool IsPushGroupOnline(string key)
        {
            bool result = false;

            dicHybirdLock.Enter( );

            if (dictPushClients.ContainsKey( key )) result = true;

            dicHybirdLock.Leave( );

            return result;
        }

        private void AddPushKey(string key)
        {
            dicHybirdLock.Enter( );

            if (!dictPushClients.ContainsKey( key ))
            {
                dictPushClients.Add( key, new PushGroupClient( ) );
            }

            dicHybirdLock.Leave( );
        }

        private PushGroupClient GetPushGroupClient(string key)
        {
            PushGroupClient result = null;
            dicHybirdLock.Enter( );

            if (dictPushClients.ContainsKey( key )) result = dictPushClients[key];

            dicHybirdLock.Leave( );

            return result;
        }

        /// <summary>
        /// 移除客户端的数据信息
        /// </summary>
        /// <param name="key"></param>
        /// <param name="clientID"></param>
        private void RemoveGroupOnlien( string key, string clientID )
        {
            GetPushGroupClient( key )?.RemovePushClient( clientID );
        }


        private void SendString(AppSession appSession,string content)
        {
            OperateResult result = SendStringAndCheckReceive( appSession.WorkSocket, 0, content );
            if(!result.IsSuccess)
            {
                RemoveGroupOnlien( appSession.KeyGroup, appSession.ClientUniqueID );
            }
        }

        #endregion

        #region Private Member

        private Dictionary<string, PushGroupClient> dictPushClients;         // 系统的数据词典
        private SimpleHybirdLock dicHybirdLock;                              // 词典锁
        private Action<AppSession, string> sendAction;                       // 发送数据的委托

        #endregion

        #region Object Override

        /// <summary>
        /// 获取本对象的字符串表示形式
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "NetPushServer";
        }


        #endregion
    }

}
