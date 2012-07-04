using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text.RegularExpressions;

namespace libirc {
    public class IRCMessageEventArgs : EventArgs {
        public IRCChannel Channel;
        public IRCUser User;
        public string Message;
    }

    public class IRCPrivateMessageEventArgs : EventArgs {
        public IRCUser User;
        public string Message;
    }

    public class IRCNicknameChangedEventArgs : EventArgs {
        public IRCUser User;
        public string OldNick;
        public string NewNick;
    }

    public class IRCTopicChangedEventArgs : EventArgs {
        public IRCUser User;
        public string OldTopic;
        public string NewTopic;
    }

    public class IRC {
        public bool Debug = false;

        public string ServerAddress;
        public int ServerPort;

        private string _Nickname;
        public string Nickname {
            get { return _Nickname; }
            set {
                this._Nickname = value;
                this.tcpWriter.WriteLine("NICK " + value);
            }
        }
        public string Username;
        public string UserModes;

        public List<IRCChannel> Channels = new List<IRCChannel>();

        public Thread LoopThread;
        public TcpClient tcpClient;
        public StreamWriter tcpWriter;
        public StreamReader tcpReader;

        public bool IsConnected { get { return this.tcpWriter != null; } }
        public bool IsReady = false;

        public delegate void IRCMessageHandler(object sender, IRCMessageEventArgs e);
        public event IRCMessageHandler MessageReceived;

        public delegate void IRCPrivateMessageHandler(object sender, IRCPrivateMessageEventArgs e);
        public event IRCPrivateMessageHandler PrivateMessageReceived;

        public delegate void IRCNicknameChangedHandler(object sender, IRCNicknameChangedEventArgs e);
        public event IRCNicknameChangedHandler NicknameChanged;

        public delegate void IRCTopicChangedHandler(object sender, IRCTopicChangedEventArgs e);
        public event IRCTopicChangedHandler TopicChanged;

        /// <summary>
        /// Connect to a new IRC server.
        /// </summary>
        /// <param name="ServerAddress">The address for the server.</param>
        /// <param name="ServerPort">The server port. Default is 6667.</param>
        /// <param name="Nickname">The nickname which to connect with.</param>
        public IRC(string ServerAddress, int ServerPort, string Nickname) : this(ServerAddress, ServerPort, Nickname, Nickname, Nickname) { }

        /// <summary>
        /// Connect to a new IRC server.
        /// </summary>
        /// <param name="ServerAddress">The address for the server.</param>
        /// <param name="ServerPort">The server port. Default is 6667.</param>
        /// <param name="Nickname">The nickname which to connect with.</param>
        /// <param name="Username">The username which to connect with. This will show up to other users as [Nickname]!~[Username]@[Host]</param>
        /// <param name="RealName">The real name which shows up in a whowas.</param>
        public IRC(string ServerAddress, int ServerPort, string Nickname, string Username, string RealName) {
            this.ServerAddress = ServerAddress;
            this.ServerPort = ServerPort;

            this._Nickname = Nickname;
            this.Username = Username;

            this.tcpClient = new TcpClient();
            this.tcpClient.Connect(ServerAddress, ServerPort);

            this.tcpWriter = new StreamWriter(this.tcpClient.GetStream()) { AutoFlush = true };
            this.tcpReader = new StreamReader(this.tcpClient.GetStream());

            this.tcpWriter.WriteLine("NICK " + Nickname);
            this.tcpWriter.WriteLine("USER " + Username + " csharp csharp :" + RealName);

            this.LoopThread = new Thread(this.ReadLoop);
            this.LoopThread.Start();
        }

        /// <summary>
        /// Get the IRCChannel object of a previously joined channel.
        /// </summary>
        /// <param name="Name">The name of the channel.</param>
        /// <returns>The IRCChannel object, or null if the channel is not joined</returns>
        public IRCChannel GetChannel(string Name) {
            foreach (IRCChannel channel in this.Channels) {
                if (channel.Name == Name)
                    return channel;
            }
            return null;
        }

        /// <summary>
        /// Get the IRCUser object of a user on the server. The specified nickname has to be in one of the joined channels.
        /// </summary>
        /// <param name="Nick">The nickname of the user to return.</param>
        /// <returns>The IRCUser object of the user on the server or null when the user can't be found.</returns>
        public IRCUser GetUser(string Nick) {
            IRCUser theUser;
            foreach (IRCChannel channel in this.Channels) {
                theUser = channel.GetUser(Nick);
                if (theUser != null)
                    return theUser;
            }
            return null;
        }

        /// <summary>
        /// Get an array of IRCUser objects which have the current nick on all channels.
        /// </summary>
        /// <param name="Nick">The nickname of the user to return.</param>
        /// <returns>The IRCUser array from all channels.</returns>
        public IRCUser[] GetUserOnChannels(string Nick) {
            List<IRCUser> ret = new List<IRCUser>();
            IRCUser theUser;
            foreach (IRCChannel channel in this.Channels) {
                theUser = channel.GetUser(Nick);
                if (theUser != null)
                    ret.Add(theUser);
            }
            return ret.ToArray();
        }

        private void ReadLoop() {
            while (IsConnected) {
                if (!this.tcpReader.EndOfStream) {
                    string Line = this.tcpReader.ReadLine();
                    string[] LineSplit = Line.Split(' ');

                    IRCChannel theChannel;
                    string theNick;
                    IRCUser theUser;
                    IRCUser[] theUsers;
                    string theTopic;

                    if (Line.StartsWith("PING :")) // Ping
                        this.tcpWriter.WriteLine("PONG :" + Line.Split(':')[1]);
                    else if (Line.StartsWith(":" + this.Nickname + " MODE " + this.Nickname + " :")) // Usermodes
                    {
                        this.UserModes = Line.Split(':')[2];
                        this.IsReady = true;
                    } else if (LineSplit[1] == "332") // Channel Joined
                    {
                        theChannel = this.GetChannel(LineSplit[3]);
                        theChannel.Topic = LineSplit[4].Substring(1);
                    } else if (LineSplit[1] == "353") // User list
                    {
                        theChannel = this.GetChannel(LineSplit[4]);

                        for (int i = 6; i < LineSplit.Length - 1; i++) {
                            theNick = LineSplit[i];

                            Regex UsernameRegex = new Regex("^(~|@|%|\\+|)(.+)");
                            Match UsernameMatch = UsernameRegex.Match(theNick);

                            theUser = new IRCUser(this);
                            theUser.Nick = UsernameMatch.Groups[UsernameMatch.Groups.Count - 1].Value;
                            theUser.Sign = UsernameMatch.Groups[1].Value;

                            theChannel._Users.Add(theUser);
                        }

                        theChannel.UsersReady = true;
                    } else if (LineSplit[1] == "PRIVMSG") // Message received
                    {
                        if (LineSplit[2].StartsWith("#")) {
                            theChannel = this.GetChannel(LineSplit[2]);
                            theUser = theChannel.GetUser(LineSplit[0].Split(':')[1].Split('!')[0]);
                            if (this.MessageReceived != null) {
                                this.MessageReceived.Invoke(this, new IRCMessageEventArgs() {
                                    Channel = theChannel,
                                    User = theUser,
                                    Message = Line.Split(new char[] { ':' }, 3)[2]
                                });
                            }
                        } else {
                            theUser = this.GetUser(LineSplit[0].Substring(1).Split('!')[0]);
                            if (theUser != null) {
                                if (this.PrivateMessageReceived != null) {
                                    this.PrivateMessageReceived.Invoke(this, new IRCPrivateMessageEventArgs() {
                                        User = theUser,
                                        Message = Line.Split(new char[] { ':' }, 3)[2]
                                    });
                                }
                            }
                        }
                    } else if (LineSplit[1] == "NICK") // Nick change
                    {
                        theNick = LineSplit[0].Substring(1).Split('!')[0];

                        theUsers = this.GetUserOnChannels(theNick);
                        foreach (IRCUser user in theUsers)
                            user.Nick = LineSplit[2].Substring(1);

                        if (this.NicknameChanged != null) {
                            this.NicknameChanged.Invoke(this, new IRCNicknameChangedEventArgs() {
                                User = theUsers[0],
                                OldNick = theNick,
                                NewNick = theUsers[0].Nick
                            });
                        }
                    } else if (LineSplit[1] == "TOPIC") // Topic change
                    {
                        theNick = LineSplit[0].Substring(1).Split('!')[0];
                        theChannel = this.GetChannel(LineSplit[2]);
                        theTopic = LineSplit[3].Substring(1);

                        theUser = this.GetUser(theNick);

                        if (this.TopicChanged != null) {
                            string oldTopic = theChannel.Topic;
                            theChannel.Topic = theTopic;
                            this.TopicChanged.Invoke(this, new IRCTopicChangedEventArgs() {
                                User = theUser,
                                OldTopic = oldTopic,
                                NewTopic = theChannel.Topic
                            });
                        }
                    } else {
                        if (this.Debug) {
                            Console.WriteLine("*UHP*: " + Line);
                        }
                    }
                }
                Thread.Sleep(5);
            }
        }

        private void NeedsReady() {
            while (!this.IsReady) { Thread.Sleep(1); }
        }

        /// <summary>
        /// Quit from the IRC server with the given message.
        /// </summary>
        /// <param name="Message">The quit message.</param>
        public void Quit(string Message) {
            if (IsConnected) {
                this.LoopThread.Abort();

                this.tcpWriter.WriteLine("QUIT :" + Message);
                this.tcpClient.Close();

                this.tcpWriter = null;
            }
        }

        /// <summary>
        /// Join a channel.
        /// </summary>
        /// <param name="Channel">The name of the channel which to join, including the #-sign.</param>
        /// <returns>The channel object joined</returns>
        public IRCChannel Join(string Channel) {
            if (IsConnected) {
                NeedsReady();

                IRCChannel newChannel = new IRCChannel(this);
                newChannel.Name = Channel;

                this.Channels.Add(newChannel);
                this.tcpWriter.WriteLine("JOIN " + Channel);

                return newChannel;
            } else
                return null;
        }

        /// <summary>
        /// Sends a message to a given user or channel.
        /// </summary>
        /// <param name="To">The receiving user or channel. In case of a channel, include the #-sign.</param>
        /// <param name="Message">The content of your message.</param>
        public void PrivMsg(string To, string Message) {
            if (IsConnected) {
                NeedsReady();
                this.tcpWriter.WriteLine("PRIVMSG " + To + " :" + Message);
            }
        }
    }
}
