using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
namespace QueryCache
{
	class MainClass
	{
		private static byte[] infoCache;
		private static byte[][] playerCache; // Jagged arrays for storing muti-packet responses.
		private static byte[][] rulesCache;
		private static byte[] challengeCode = new byte[4];
		private const int maxPacket = 1400;
		private static int infoQueries;
		private static int otherQueries;
		private static DateTime lastInfoTime;
		private static DateTime lastPlayersTime;
		private static DateTime lastRulesTime;
		private static DateTime lastPrint;
		private static IPEndPoint serverEP;
		private static Socket serverSock = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		private static Socket publicSock = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

		public static byte[] RequestChallenge()
		{
			if (challengeCode [0] == 0xFF && challengeCode [1] == 0xFF && challengeCode [2] == 0xFF && challengeCode [3] == 0xFF) { //Check if we've still got the default challenge of 'FF FF FF FF'

				byte[] requestPacket = new byte[9];
				requestPacket [0] = 0xFF;
				requestPacket [1] = 0xFF;
				requestPacket [2] = 0xFF;
				requestPacket [3] = 0xFF;
				requestPacket [4] = 0x55;
				requestPacket [5] = 0xFF;
				requestPacket [6] = 0xFF;
				requestPacket [7] = 0xFF;
				requestPacket [8] = 0xFF;
				byte[] serverResponse = new byte[9];
				serverSock.SendTimeout = 100;
				serverSock.ReceiveTimeout = 1000;
				try{
					serverSock.SendTo (requestPacket, serverEP);
				}catch{
					return challengeCode;
				}

				try {
					serverSock.Receive(serverResponse);
				}catch{
					return challengeCode;
				}

				if (serverResponse [4] == 0x41) { // Check the header to see if the server sent a challenge code.
					System.Buffer.BlockCopy (serverResponse, 5, challengeCode, 0, 4); // Copy the challenge code back in to our variable for re-use
					return challengeCode;
				} else {
				}

			}
			return challengeCode; // Return the cached code we got previously
		}

		public static byte[] BuildRequest(byte queryType)
		{
			byte[] builtQuery = new byte[9];
			builtQuery [0] = 0xFF;
			builtQuery [1] = 0xFF;
			builtQuery [2] = 0xFF;
			builtQuery [3] = 0xFF;
			builtQuery [4] = queryType;
			System.Buffer.BlockCopy (RequestChallenge (), 0, builtQuery, 5, 4);

			return builtQuery;
		}

		public static bool ChallengeIsValid(byte[] requestQuery)
		{
			byte[] challengeCode = RequestChallenge ();

			for (int i = 0; i < 4; i++) {
				if (!requestQuery [i+5].Equals(challengeCode [i])) {
					return false;
				}
			}
			return true;
		}


		public static int UpdateCache(byte queryType)
		{
			if (queryType == 0x54) {
				// A2S info queries don't need a challenge code
				// We'll build the query packet and send it.
				string queryString = "Source Engine Query";
				byte[] queryStringBytes = new byte[6 + queryString.Length];
				queryStringBytes [0] = 0xFF;
				queryStringBytes [1] = 0xFF;
				queryStringBytes [2] = 0xFF;
				queryStringBytes [3] = 0xFF;
				queryStringBytes [4] = 0x54;
				queryStringBytes [queryStringBytes.Length - 1] = 0x00;
				System.Buffer.BlockCopy (Encoding.Default.GetBytes (queryString), 0, queryStringBytes, 5, queryString.Length);
				try{
					serverSock.SendTo (queryStringBytes, serverEP);
				}catch{
					return 1;
				}
			} else {
				try{
					serverSock.SendTo (BuildRequest (queryType), serverEP); // Every other query type will be sent with a challenge code
				}catch{
					return 1;
				}
			}
			byte[] recvBuffer = new byte[maxPacket];
			int packetLen;
			try {
				packetLen = serverSock.Receive (recvBuffer);
			}catch{
				return 1;
			}
			// We're not expecting a challenge code to return, but just in case that it does, we'll update our cached code to the new one.
			if (recvBuffer[0] == 0xFF && recvBuffer[1] == 0xFF && recvBuffer[2] == 0xFF && recvBuffer[3] == 0xFF && recvBuffer[4] == 0x41){
				System.Buffer.BlockCopy (recvBuffer, 5, challengeCode, 0, 4);
				return UpdateCache (queryType);
			}

			if (recvBuffer[0] == 0xFE){
				// Check first byte in the packet that indicates a multi-packet response.
				int packetCount = Convert.ToInt32 (recvBuffer [8]); // Total number of packets that the server is sending will be at this position.
				switch(recvBuffer[16]){ // The packet type header will be at this position for the first packet.
				case 0x45: // Returned rules list header
					rulesCache = new byte[packetCount][]; // Initialise our array with same size as the number of packets
					for (int i = 0; i < packetCount; i++) {
						rulesCache [i] = new byte[packetLen];
						System.Buffer.BlockCopy (recvBuffer, 0, rulesCache [i], 0, packetLen); // Dump our packet contents in to its place
						if (i < packetCount - 1) { // Get ready to receive next packet if this isn't the last iteration.
							recvBuffer = new byte[maxPacket];
							try {
								packetLen = serverSock.Receive (recvBuffer);
							} catch {
								return 1;
							}
						}
					}
					return 0;

				case 0x44: // Returned player list header
					playerCache = new byte[packetCount][];
					for (int i = 0; i < packetCount; i++) {
						playerCache [i] = new byte[packetLen];
						System.Buffer.BlockCopy (recvBuffer, 0, playerCache[i], 0, packetLen);
						if (i < packetCount - 1) {
							recvBuffer = new byte[maxPacket];
							try{
								packetLen = serverSock.Receive (recvBuffer);
							}catch{
								return 1;
							}
						}
					}
					return 0;

				}
				return 1; // We didn't match anything that we can handle :(
			}else{
				// Handle single packet response.
				switch (recvBuffer[4]) {
				case 0x49:
					infoCache = new byte[packetLen];
					System.Buffer.BlockCopy (recvBuffer, 0, infoCache, 0, packetLen);
					return 0;
				case 0x44:
					playerCache = new byte[1][]; // Initialise our array with a single slot because we only have a single packet to store.
					playerCache [0] = new byte[packetLen];
					System.Buffer.BlockCopy (recvBuffer, 0, playerCache[0], 0, packetLen);
					return 0;
				case 0x45:
					rulesCache = new byte[1][];
					rulesCache [0] = new byte[packetLen];
					System.Buffer.BlockCopy (recvBuffer, 0, rulesCache[0], 0, packetLen);
					return 0;
				}
				/*infoCache = new byte[recvBuffer.Length];
				playerCache = new byte[1][];
				playerCache [0] = new byte[recvBuffer.Length];
				rulesCache = new byte[1][];
				rulesCache [0] = new byte[recvBuffer.Length];*/
				return 1;
			}
		}

		public static void Main (string[] args)
		{
			string cmdLine = Environment.GetCommandLineArgs () [0];
			if (args.Length != 3) {
				Console.WriteLine ("Usage: " + cmdLine + " <proxy port> <gameserver ip> <gameserver port>");
				Environment.Exit (1);
			}
			IPAddress targetIP;
			int targetPort;
			int localPort;
			if (!int.TryParse (args [0], out localPort) || localPort < 1 || localPort > 65535) {
				Console.WriteLine ("Invalid proxy port!");
				Environment.Exit (2);
			}
			if (!IPAddress.TryParse (args [1], out targetIP)) {
				Console.WriteLine ("Invalid gameserver IP address!");
				Environment.Exit (3);
			}
			if (!int.TryParse (args [2], out targetPort) || targetPort < 1 || targetPort > 65535) {
				Console.WriteLine ("Invalid gameserver port!");
				Environment.Exit (4);
			}

			serverEP = new IPEndPoint (targetIP, targetPort);
			IPEndPoint localEndPoint = new IPEndPoint (IPAddress.Any, localPort);
			IPEndPoint sendingIPEP = new IPEndPoint (IPAddress.Any, 0);
			EndPoint requestingEP = (EndPoint)sendingIPEP;

			try {
				publicSock.Bind (localEndPoint);
			} catch {
				Console.WriteLine ("Cannot bind proxy port!");
				Environment.Exit (5);
			}
			serverSock.Bind (new IPEndPoint (IPAddress.Any, 0));

			// Give our intial challenge code a value
			challengeCode [0] = 0xFF;
			challengeCode [1] = 0xFF;
			challengeCode [2] = 0xFF;
			challengeCode [3] = 0xFF;

			// Give our time trackers an initial time
			lastInfoTime = DateTime.Now - TimeSpan.FromSeconds(30);
			lastPlayersTime = DateTime.Now - TimeSpan.FromSeconds (30);
			lastRulesTime = DateTime.Now - TimeSpan.FromSeconds(30);
			lastPrint = DateTime.Now;

			// Main query receiving loop
			while (true) {

				byte[] reqPacket = new byte[maxPacket];
				try{
					publicSock.ReceiveFrom (reqPacket, ref requestingEP);
				}catch{
					continue;
				}
				switch (reqPacket [4]) {
				case 0x54: // Info Queries
					infoQueries++;
					if (lastInfoTime + TimeSpan.FromSeconds(5) <= DateTime.Now){
						// Update our cached values
						if (UpdateCache(reqPacket[4]) > 0){break;};
						// Successful update means we reset the timer
						lastInfoTime = DateTime.Now;

					}
					// If we get this far, we send our cached values.
					try{
						publicSock.SendTo(infoCache, requestingEP);
					}catch{
						continue;
					}
					break;

				case 0x55: // Player list
					otherQueries++;
					if (!ChallengeIsValid (reqPacket)) { // We check that the client is using the correct challenge code
						try{
							publicSock.SendTo (BuildRequest (0x41), requestingEP);
						}catch{
							continue;
						}// We'll send the client the correct challenge code to use.
						break;
					}
					if (lastPlayersTime + TimeSpan.FromSeconds (3) <= DateTime.Now) {
						if (UpdateCache(reqPacket[4]) > 0){break;};
						lastPlayersTime = DateTime.Now;
					}
					for (int i = 0; i < playerCache.Length; i++) {
						try{
							publicSock.SendTo(playerCache[i], requestingEP);
						}catch{
							continue;
						}
					}
					break;

				case 0x56: //Rules list
					otherQueries++;
					if (!ChallengeIsValid (reqPacket)) {
						try{
							publicSock.SendTo (BuildRequest (0x41), requestingEP);
						}catch{
							continue;
						}
						break;
					}
					if (lastRulesTime + TimeSpan.FromSeconds (10) <= DateTime.Now) {
						if (UpdateCache(reqPacket[4]) > 0){break;};
						lastRulesTime = DateTime.Now;
					}
					for (int i = 0; i < rulesCache.Length; i++) {
						try{
							publicSock.SendTo(rulesCache[i], requestingEP);
						}catch{
							continue;
						}
					}
					break;

				case 0x57: // Challenge request
					otherQueries++;
					try{
						publicSock.SendTo (BuildRequest (0x41), requestingEP); // Send challenge response.
					}catch{
						continue;
					}
					break;
				}

				if ((DateTime.Now.AddSeconds(-10) >= lastPrint) ) {
					Console.WriteLine ("{0} info queries and {1} other queries in last {2} seconds", infoQueries, otherQueries,(DateTime.Now - lastPrint).Seconds);
					infoQueries = 0;
					otherQueries = 0;
					lastPrint = DateTime.Now;
				}
			}
		}
	}
}
