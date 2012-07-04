using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace libirc
{
    public class IRCUser
    {
        private IRC Parent;

        public string Nick;
        public string Sign;
        public string Mode;

        /// <summary>
        /// Returns the full nick with any optional sign before it.
        /// </summary>
        public string FullNick
        {
            get { return this.Sign + this.Nick; }
        }

        /// <summary>
        /// Makes a new IRCUser object with the given IRC object as it's server.
        /// </summary>
        /// <param name="parent">The IRC object that represents the server connection.</param>
        public IRCUser(IRC parent)
        {
            this.Parent = parent;
        }

        /// <summary>
        /// Sends a private message to this user.
        /// </summary>
        /// <param name="Message"></param>
        public void SendMessage(string Message)
        {
            this.Parent.PrivMsg(this.Nick, Message);
        }
    }
}
