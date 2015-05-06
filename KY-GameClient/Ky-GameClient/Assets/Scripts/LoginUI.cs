using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using Lidgren;
using Lidgren.Network;
using System.Linq;

public class LoginUI : MonoBehaviour {

	//Scene identifiers for reference
	//LoginScreen Scene = Scene 0
	//Twin City Scene   = Scene 1
	//Gotham Scene      = Scene 2
	//Bird Island Scene = Scene 3


	//The prefab which reflects other players
	public Transform prefab;

	//Textfields
	public InputField username;
	public InputField zone;
	public InputField address;
	
	//Player info
	public Character player;
	public List<Character> players;
	public List<gameObject> objects; //Won't use it in this example, but in case your zone needs items?
	public float speed = 6.0f;
	
	//Network objects and variables
	NetClient Client;
	NetPeerConfiguration Config = new NetPeerConfiguration("Ky");
	const int Port = 3000;
	bool ConnectedToServer = false;
	
	// Use this for initialization
	void Start () {
		//This holds all our network code, let's not break it. it's useful
		DontDestroyOnLoad (GameObject.Find("ScriptHolder"));
	}
	
	// Update is called once per frame
	void FixedUpdate () {

		if (ConnectedToServer) {
			ProcessMovementInput();
			ReadIncomingServerMessages ();
		}
	}
	
	//This function is called when we click on the login button
	public void LoginToServer() {
		
		//Cheap but yeah, let the user select a zone to start in, but verify a proper zone has been typed in.
		if (zone.text != "Twin City") {
			print ("Dont be a faggot, type it in right >.>");
			return;
		}
		
		//Explained in the server code
		Config.EnableMessageType (NetIncomingMessageType.ConnectionApproval);
		//Instantiate new net client with the given config
		Client = new NetClient (Config);
		
		NetOutgoingMessage outmsg = Client.CreateMessage ();
		Client.Start ();
		
		//Instantiate our character locally
		player = new Character ();
		player.Name = username.text;
		player.Skin = "Blue"; //idk
		
		//Set zone according to whatever we choose to log in to
		if (zone.text == "Twin City")
			player.CurrentZone = Zones.TwinCity;
		else if (zone.text == "Bird Island")
			player.CurrentZone = Zones.BirdIsland;
		
		//Create the players list, so we can add players to it once we are connected and receive.. players from the server
		players = new List<Character> ();
		
		//Write login bite first because we're logging in
		outmsg.Write ((byte)PacketTypes.LOGIN);
		//Write all of our players info to the message
		outmsg.WriteAllProperties (player);
		//Connect to the server & send the message we wrote along side it
		Client.Connect (address.text, Port, outmsg);
		
		//We sent the login info, now wait for the server to reply
		WaitForLoginResponse ();
	}
	
	
	void WaitForLoginResponse()
	{
		bool ResponseReceived = false;
		
		NetIncomingMessage inc;
		
		//Not sure if it's smart to use While() in unity, but bare with me.
		while (!ResponseReceived) {
			//Wait until we receive a message
			if ((inc = Client.ReadMessage ()) != null) {
				switch (inc.MessageType) {
					
				case NetIncomingMessageType.StatusChanged:
					if (inc.SenderConnection.Status == NetConnectionStatus.Disconnected) {
						inc.ReadByte ();
						if(inc.ReadString() == "Already Connected")
						{
							print("You're already connected.");
							return;
						}
					}
					break;
					
				case NetIncomingMessageType.Data:
					
					if(inc.ReadByte() == (byte)PacketTypes.WORLDSTATE)
					{
						players.Clear();
						
						//First receive the amount of players
						int AmountOfPlayersInThisZone = inc.ReadInt32();
						
						//Read Read n^ amount of messages, one for every player
						for(int i = 0; i < AmountOfPlayersInThisZone; i++)
						{
							//Create a new 'Character' instance for each player in our zone
							Character character = new Character();
							//Read the info about this player and write the properties into our character class
							inc.ReadAllProperties(character);
							//Add the player to our zones' list
							players.Add(character);
						}
						
						//Note! ::
						//~*~*~*~ Create more loops to receive NPC locations, item locations, mobs, etc.. But for now we will just receive players ~*~*~*~
						
						//When this bool is set to true our update() function will start reading incoming messages from the server
						ConnectedToServer = true;
						//To break the loop, though I could just use 'break'.. whatever. it's synthetic sugar
						ResponseReceived = true; 
						
						//Here we will load the scene in which we logged in to. Just doing twin city for now because I'm a lazy douchebag.
						if(zone.text == "Twin City")
							Application.LoadLevel(1);
						
					}
					break;
				}
			}
		}
	}

	void TellServerAboutMyNewCoordinates() {
		//Sends our updates position to the server, so the server can tell other clients about my new position.
		NetOutgoingMessage outmsg = Client.CreateMessage ();
		outmsg.Write ((byte)PacketTypes.MOVE);
		outmsg.Write (player.Coordinates.x);
		outmsg.Write (player.Coordinates.y);
		Client.SendMessage (outmsg, NetDeliveryMethod.ReliableOrdered);
	}


	void ProcessMovementInput() {
		//Basic movement, duh
		Vector3 move = new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), 0);
		//Calculate new position based on input
		player.Coordinates += move * speed * Time.deltaTime; //Maybe you know a more efficient way, i'm not too familiar with unity controls
		//Assign new position to object
		GameObject.Find ("player").transform.position = player.Coordinates;

		//^- Seems really inefficient but, I'm just not sure how unity works just yet. Feel free to improve

		//Update server with new position when done

		//Ok normally you should only run this function when there has actually been a change in movement, but bit sleepy right now.
		//Will probably fix/add later.
		TellServerAboutMyNewCoordinates ();
	}


	//The balls of this setup. This function will
	//Process all incoming messages and update stuff in our world based on the messages received
	void ReadIncomingServerMessages()
	{
		NetIncomingMessage inc;
		
		if((inc = Client.ReadMessage()) != null){ 
			
			if (inc.MessageType == NetIncomingMessageType.Data) {
				byte packetType = inc.ReadByte();
				
				//World state packets are more to update everything in the current zone/world, it's highly inefficient to do this-
				//constantly so just do this every.. few seconds to make sure everything stays in sync (not implemented yet)
				
				switch(packetType)
				{

				case (byte)PacketTypes.WORLDSTATE:
					players.Clear();
					
					//First receive the amount of players
					int AmountOfPlayersInThisZone = inc.ReadInt32();
					
					//Read Read n^ amount of messages, one for every player
					for(int i = 0; i < AmountOfPlayersInThisZone; i++)
					{
						//Create a new 'Character' instance for each player in our zone
						Character character = new Character();
						//Read the info about this player and write the properties into our character class
						inc.ReadAllProperties(character);
						//Add the player to our zones' list
						players.Add(character);
					}

					InstantiateConnectedPlayers();
					break;

				case (byte)PacketTypes.MOVEMENTUPDATE:

					//Find the player which we want to update
					Character playerToUpdate = players.Where(f => f.Name == inc.ReadString()).FirstOrDefault();
					//Set their coordinates
					GameObject.Find(playerToUpdate.Name).transform.position = new Vector3(inc.ReadFloat(), inc.ReadFloat()); //Find probably inefficient, I gotta try other stuff later.

					break;
				}
			}
		}	
	}

	void InstantiateConnectedPlayers()
	{
		foreach (Character player in players) {
			Instantiate(prefab, player.Coordinates, Quaternion.identity).name = player.Name;
		}
	}
}

	public class gameObject
	{
		public string Name { get; set; }
		public Vector3 Coordinates { get; set; }
	}
	
	public class Zone
	{
		public string Zones { get; set; }
		public int MaxPlayers { get; set; }
	}
	
	public class Character
	{
		public string Name { get; set; }
		public string Skin { get; set; }
		public Zones CurrentZone { get; set; }
		public Vector3 Coordinates { get; set; }
		public NetConnection Connection { get; set; }

		public Character(string name, Vector3 coordinates, string charskin, NetConnection conn)
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
	
	public enum Zones
	{
		BirdIsland,
		TwinCity,
		Gotham
	}
	
	public enum PacketTypes
	{
		LOGIN,
		MOVE,
		ZONESWITCH,
		WORLDSTATE,
		CHAT,
		MOVEMENTUPDATE
	}