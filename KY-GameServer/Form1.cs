using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Lidgren.Network;

namespace KY_GameServer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        //Network properties
        NetServer Server { get; set; }
        NetPeerConfiguration Config { get; set; }
        DateTime time { get; set; }
        TimeSpan timeToPass { get; set; }
        NetIncomingMessage inc { get; set; }
        bool isServerRunning { get; set; }

        private void button1_Click(object sender, EventArgs e)
        {

        }
    }
}
