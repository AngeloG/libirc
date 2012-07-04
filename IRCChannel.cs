using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace libirc
{
    public class IRCChannel
    {
        private IRC Parent;

        public string Name;
        public string Mode;
        public string Topic;

        internal List<IRCUser> _Users = new List<IRCUser>();
        public List<IRCUser> Users
        {
            get
            {
                while (!this.UsersReady) { System.Threading.Thread.Sleep(1); }
                return _Users;
            }
            set { _Users = value; }
        }

        internal bool UsersReady;

        /// <summary>
        /// Makes a new IRC channel object with the given IRC object as it's server.
        /// </summary>
        /// <param name="parent">The IRC object that represents the server connection.</param>
        public IRCChannel(IRC parent)
        {
            this.Parent = parent;
        }

        /// <summary>
        /// Get the user in this channel.
        /// </summary>
        /// <param name="Nick">The nickname of the user</param>
        /// <returns>The IRC user with the given nickname or null if there's no such user in this channel</returns>
        public IRCUser GetUser(string Nick)
        {
            foreach (IRCUser user in this.Users)
            {
                if (user.Nick == Nick)
                    return user;
            }
            return null;
        }

        /// <summary>
        /// Send a message to this channel.
        /// </summary>
        /// <param name="Message">The message to be sent.</param>
        public void SendMessage(string Message)
        {
            this.Parent.PrivMsg(this.Name, Message);
        }
    }
}
