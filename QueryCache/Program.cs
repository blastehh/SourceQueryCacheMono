using System;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
namespace QueryCache
{
	class MainClass
	{
		private static byte[] infoCache;
		private static byte[][] playerCache;
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

		public static byte[] RequestChallenge()
		{
			if (challengeCode [0] == 0xFF && challengeCode [1] == 0xFF && challengeCode [2] == 0xFF && challengeCode [3] == 0xFF) {

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
				serverSock.SendTimeout = 50;
				serverSock.ReceiveTimeout = 1000;
				serverSock.SendTo (requestPacket, serverEP);

				try {
					serverSock.Receive(serverResponse);
				}catch{
					return challengeCode;
				}

				if (serverResponse [4] == 0x41) {
					System.Buffer.BlockCopy (serverResponse, 5, challengeCode, 0, 4);
					return challengeCode;
				} else {
				}

			}
			return challengeCode;
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

			for (int i = 0; i < 3; i++) {
				if (!requestQuery [i+5].Equals(challengeCode [i])) {
					return false;
				}
			}
			return true;
		}


		public static int UpdateCache(byte queryType)
		{
			if (queryType == 0x54) {
				// send query
				string queryString = "Source Engine Query";
				byte[] queryStringBytes = new byte[6 + queryString.Length];
				queryStringBytes [0] = 0xFF;
				queryStringBytes [1] = 0xFF;
				queryStringBytes [2] = 0xFF;
				queryStringBytes [3] = 0xFF;
				queryStringBytes [4] = 0x54;
				queryStringBytes [queryStringBytes.Length - 1] = 0x00;
				System.Buffer.BlockCopy (Encoding.Default.GetBytes (queryString), 0, queryStringBytes, 5, queryString.Length);
				serverSock.SendTo (queryStringBytes, serverEP);
			} else {
				serverSock.SendTo (BuildRequest (queryType), serverEP);
			}
			byte[] recvBuffer = new byte[maxPacket];
			int packetLen;
			try {
				packetLen = serverSock.Receive (recvBuffer);
			}catch{
				return 1;
			}
				
			if (recvBuffer[0] == 0xFF && recvBuffer[1] == 0xFF && recvBuffer[2] == 0xFF && recvBuffer[3] == 0xFF && recvBuffer[4] == 0x41){
				System.Buffer.BlockCopy (recvBuffer, 5, challengeCode, 0, 4);
				UpdateCache (queryType);
			}

			if (recvBuffer[0] == 0xFE){
				//multiple packets!
				int packetCount = Convert.ToInt32 (recvBuffer [8]);
				switch(recvBuffer[16]){
				case 0x45:
					rulesCache = new byte[packetCount][];
					for (int i = 0; i < packetCount; i++) {
						rulesCache [i] = new byte[packetLen];
						System.Buffer.BlockCopy (recvBuffer, 0, rulesCache[i], 0, packetLen);
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

				case 0x44:
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

			}else{
				
				switch (recvBuffer[4]) {
				case 0x49:
					infoCache = new byte[packetLen];
					System.Buffer.BlockCopy (recvBuffer, 0, infoCache, 0, packetLen);
					return 0;
				case 0x44:
					playerCache = new byte[1][];
					playerCache [0] = new byte[packetLen];
					System.Buffer.BlockCopy (recvBuffer, 0, playerCache[0], 0, packetLen);
					return 0;
				case 0x45:
					rulesCache = new byte[1][];
					rulesCache [0] = new byte[packetLen];
					System.Buffer.BlockCopy (recvBuffer, 0, rulesCache[0], 0, packetLen);
					return 0;
				}
				infoCache = new byte[recvBuffer.Length];
				playerCache = new byte[1][];
				playerCache [0] = new byte[recvBuffer.Length];
				rulesCache = new byte[1][];
				rulesCache [0] = new byte[recvBuffer.Length];
				return 1;
			}
			return 1;
		}

		public static void Main (string[] args)
		{
			string cmdLine = Environment.CommandLine;
			if (args.Length != 3) {
				Console.WriteLine ("Usage: " + cmdLine + " <proxy port> <gameserver ip> <gameserver port>");
				Environment.Exit (1);
			}
			serverEP = new IPEndPoint (IPAddress.Parse(args [1]), int.Parse(args [2]));

			challengeCode [0] = 0xFF;
			challengeCode [1] = 0xFF;
			challengeCode [2] = 0xFF;
			challengeCode [3] = 0xFF;

			IPEndPoint localEndPoint = new IPEndPoint (IPAddress.Any, int.Parse( args [0]));
			IPEndPoint sendingIPEP = new IPEndPoint (IPAddress.Any, 0);
			EndPoint requestingEP = (EndPoint)sendingIPEP;

			serverSock.Bind (new IPEndPoint (IPAddress.Any, 0));
			Socket publicSock = new Socket (AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

			try {
				publicSock.Bind (localEndPoint);
			} catch {
				Console.WriteLine ("Cannot bind port!");
				Environment.Exit (2);
			}

			lastInfoTime = DateTime.Now - TimeSpan.FromSeconds(30);
			lastPlayersTime = DateTime.Now - TimeSpan.FromSeconds (30);
			lastRulesTime = DateTime.Now - TimeSpan.FromSeconds(30);
			lastPrint = DateTime.Now;

			while (true) {
				
				byte[] reqPacket = new byte[maxPacket];
				publicSock.ReceiveFrom (reqPacket, ref requestingEP);
				switch (reqPacket [4]) {
				case 0x54: //Info Queries
					infoQueries++;
					if (lastInfoTime + TimeSpan.FromSeconds(5) <= DateTime.Now){
						// do the thing
						if (UpdateCache(reqPacket[4]) > 0){break;};
						//then reset the time
						lastInfoTime = DateTime.Now;

					}
					//send the cached ver
					publicSock.SendTo(infoCache, requestingEP);

					break;

				case 0x55: //Player list
					otherQueries++;
					if (!ChallengeIsValid (reqPacket)) {
						publicSock.SendTo (BuildRequest (0x41), requestingEP);
						break;
					}
					if (lastPlayersTime + TimeSpan.FromSeconds (3) <= DateTime.Now) {
						// do the thing
						if (UpdateCache(reqPacket[4]) > 0){break;};
						//then reset the time
						lastPlayersTime = DateTime.Now;
					}
					//send the cached ver
					for (int i = 0; i < playerCache.Length; i++) {
						publicSock.SendTo(playerCache[i], requestingEP);
					}
					break;

				case 0x56: //Rules list
					otherQueries++;
					if (!ChallengeIsValid (reqPacket)) {
						publicSock.SendTo (BuildRequest (0x41), requestingEP);
						break;
					}
					if (lastRulesTime + TimeSpan.FromSeconds (10) <= DateTime.Now) {
						// do the thing
						if (UpdateCache(reqPacket[4]) > 0){break;};
						//then reset the time
						lastRulesTime = DateTime.Now;
					}
					//send the cached ver
					for (int i = 0; i < rulesCache.Length; i++) {
						publicSock.SendTo(rulesCache[i], requestingEP);
					}
					break;
				case 0x57: //Get challenge
					otherQueries++;
					publicSock.SendTo (BuildRequest (0x41), requestingEP);
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
