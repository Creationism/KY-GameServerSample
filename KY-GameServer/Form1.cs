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
using System.Threading;

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
        DateTime time { get; set; } //In case we want to keep track of server time..?
        NetIncomingMessage inc { get; set; }
        bool serverIsRunning { get; set; }

        //Actual game info data
        List<Character> ConnectedPlayers { get; set; }
        List<Zone> ActiveZones { get; set; }

        private void startServer(object sender, EventArgs e)
        {
            //Create a new config instance, used to configure the server settings. Ky is the identifier for our network relationship in this case
            Config = new NetPeerConfiguration("Ky");
            //Listening on port 3000, clients must connect to port 3000 if they want to connect to us.
            Config.Port = 3000;
            //Enables it so users have to 'login'/'be approved' to join our network
            Config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            //Create new server instance with the Config we've created
            Server = new NetServer(Config);
            //[sass]I don't know what this does..[/sass]
            Server.Start();

            //Let it be known that the server has started, hallelujah
            Write("Server started on port: " + Config.Port);

            //Create our data instances to keep track of our game world
            ConnectedPlayers = new List<Character>();

            //We listen for clients on a separate Thread. This is a bit of an 'advanced' topic. You can ask me more about it if you're curious
            //But the main reason for this one is so, it does not perform these actions on our 'Main' thread. This thread is used for our UI
            //If we perform too many functions on our UI thread. Our UI may lag, so.. there's that!
            Thread t = new Thread(new ThreadStart(listenForMessages));
            t.Start();

            serverIsRunning = true;
        }

        //In our listenForMessages we listen for all the messages we receive from clients. This is kind of the 'guts' of the server.
        //Processing messages
        private void listenForMessages()
        {
            while(serverIsRunning)
            {
                //If our message buffer isn't null, it means we have received a new message
                if((inc = Server.ReadMessage()) != null) 
                {
                    //Switch statement to weed through the message types and handle each messagetype accordingly
                    switch(inc.MessageType)
                    {
                        //These cases should be self explanatory. I'll add the following;
                        //1. ConnectionApproval is if someone is trying to login
                        //2. Data is for ANYTHING game related, movement, combat, chat, anything the user wants to tell us while he's logged into our game
                        //3. Status changed if we want to capture a disconnected player and log it, can also be used for connect.. and more. 

                        case NetIncomingMessageType.ConnectionApproval:
                            byte loginbyte = inc.ReadByte();

                            if(loginbyte == (byte)PacketTypes.LOGIN)
                            {
                                newLogin();
                            }
                            break;

                        case NetIncomingMessageType.Data:
                            byte dataByte = inc.ReadByte();

                            if(dataByte == (byte)PacketTypes.MOVE)
                            {
                                updateMovements();
                            }

                            break;

                        case NetIncomingMessageType.StatusChanged:

                            //If a player is disconnected or is disconnecting.. remove him from our list
                            if (inc.SenderConnection.Status == NetConnectionStatus.Disconnected || inc.SenderConnection.Status == NetConnectionStatus.Disconnecting)
                            {
                                removeDisconnectedUser(inc.SenderConnection);
                            }

                            break;
                    }
                }
            }
        }


        //This function will manage all the movement and coordination across players (notifies other players if someone has moved, to reflect that and stuff) 
        private void updateMovements()
        {
            
        }


        private void newLogin()
        {
            //When a player logs in, we expect that he sends his character info to us. 
            //Normally he would only send his username and password but because we aren't using a database model
            //We'll just create the users character in their own client. Maybe database example next time
            //FYI: The connecting player can manually specify the zone he wants to connect to, easily changed by just setting the property though

            //Create new instance of player then read all the properties the client has sent into this class
            Character newlyConnectedPlayer = new Character();
            inc.ReadAllProperties(newlyConnectedPlayer);

            //If the player that is trying to connects' name already exists in our active player list
            //We will refuse his connection. This can be handy, if you want to allow only one character (obviously) or one
            //connection per IP Address, easy to modify
            if(ConnectedPlayers.FirstOrDefault(f => f.Name == newlyConnectedPlayer.Name) != null)
            {
                inc.SenderConnection.Deny("You're already connected");
                Write("Refused player with name " + newlyConnectedPlayer.Name + " because he was already connected.."); // LET IT BE KNOWN!
                return;
            }

            //We give our player a connection property so we can keep connection info about him, like IP Address/ping, etc!
            newlyConnectedPlayer.Connection = inc.SenderConnection;

            //Let it be known that this player has connected to our wonderful game
            Write(newlyConnectedPlayer.Name + " has connected!");

            //Approve this fine gentleman into our secret society
            inc.SenderConnection.Approve();

            //Add this fine lad to the list of connected players
            ConnectedPlayers.Add(newlyConnectedPlayer);


            //Now it gets a little more interesting! 
            //*~*~*~*~* (SPARKLES TO MAKE IT MORE MAGICAL!) *~*~*~*~*

            //Worldstate (Can specify a name yourself) messages send the current zone state to players
            
            NetOutgoingMessage outmsg = Server.CreateMessage(); //We create a new message
            outmsg.Write((byte)PacketTypes.WORLDSTATE); // Of type WORLDSTATE

            //Get the amount of players the zone the new player is in
            outmsg.Write(ConnectedPlayers.Where(f => f.CurrentZone == newlyConnectedPlayer.CurrentZone).Count()); //notice 'Count'

            //For each player in this players' zone, send him the data of the players. (The players' client will process this info and draw them on his screen)
            foreach (Character ch in ConnectedPlayers.Where(x => x.CurrentZone == newlyConnectedPlayer.CurrentZone))
            {
                outmsg.WriteAllProperties(ch);
            }
            Server.SendMessage(outmsg, inc.SenderConnection, NetDeliveryMethod.ReliableOrdered, 0); // Send the message, reliably and ordered

            // LET IT BE KNOWN!
            Write(String.Format("{0} : {1}  - has connected to the server and is in {2}", newlyConnectedPlayer.Name, 
                                                                                          newlyConnectedPlayer.Connection.RemoteEndpoint.Address, 
                                                                                          newlyConnectedPlayer.CurrentZone));

            //Update our UI
            updateList();
        }

        //Function to display users in our list overview
        private void updateList()
        {
            //This invoke stuff is a bit 'complicated', don't pay attention to it. It's just.. this function is run in a separate thread
            //You can't make changes from a different thread in the UI thread unless you invoke it.. not normally you would usually have
            //to deal with.
            this.Invoke(new MethodInvoker(delegate
            {
                dataGridView1.Rows.Clear();
                //Iterate through all connected players and add their info to our list
                foreach (Character ch in ConnectedPlayers)
                {
                    dataGridView1.Rows.Add(
                        ch.Name, 
                        ch.Connection.RemoteEndpoint.Address, 
                        ch.Coordinates,
                        ch.CurrentZone, 
                        ch.Skin, 
                        ch.Connection.AverageRoundtripTime);
                }
            }));
        }

        //Removes players from our list of connections
        private void removeDisconnectedUser(NetConnection conn)
        {
            ConnectedPlayers.Remove(ConnectedPlayers.Where(f => f.Connection.RemoteUniqueIdentifier == conn.RemoteUniqueIdentifier).FirstOrDefault());
            updateMovements();
        }

        //Simple function that allows us to log activity to our activity textbox, also known as "LET IT BE KNOWN!"
        private void Write(string msg)
        {
            this.Invoke(new MethodInvoker(delegate
            {
                textBox1.AppendText(msg + Environment.NewLine);
            }));
        }
    }

    //Class used for simple functions to keep our main class tidy
    public static class Misc
    {

    }

    class gameObject
    {
        public string Name {get; set;}
        public Point Coordinates { get; set;}
    }

    class Zone
    {
        public string Zones { get; set; }
        public int MaxPlayers { get; set; }
    }

    class Character
    {
        public string Name { get; set; }
        public string Skin { get; set; }
        public Zones CurrentZone { get; set; }
        public Point Coordinates { get; set; }
        public NetConnection Connection { get; set; }
        public Character(string name, Point coordinates, string charskin, NetConnection conn)
        {
            Skin = charskin;
            Name = name;
            Coordinates = coordinates;
            Connection = conn;
        }
        public Character()
        {

        }
    }

    enum Zones
    {
        BirdIsland,
        TwinCity,
        Gotham
    }

    enum PacketTypes
    {
        LOGIN,
        MOVE,
        WORLDSTATE,
        CHAT
    }
}
