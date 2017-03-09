using GTANetworkServer;
using GTANetworkShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace prophunt
{
	public class Prophunt : Script
	{
		public PhMetadata m_metadata = new PhMetadata();

		public List<string> m_allowedProps = new List<string>();
		public List<PhPlayer> m_players = new List<PhPlayer>();

		public PhGameState m_gameState = PhGameState.Waiting;
		public DateTime m_gameStateChanged = DateTime.Now;

		public PhEventQueue m_events;

		public static Random Rnd = new Random();
		public static Prophunt Instance;

		public static void Log(string format, params object[] args)
		{
			API.shared.consoleOutput("[Prophunt] " + format, args);
		}

		public static bool PropAllowed(string name)
		{
			return Instance.m_allowedProps.Contains(name.ToLower());
		}

		public Prophunt()
		{
			m_metadata.tmHiding = 30;
			m_metadata.tmSeeking = 120;
			m_metadata.tmEndOfRound = 10;

			m_events = new PhEventQueue(API);

			API.onResourceStart += API_onResourceStart;
			API.onResourceStop += API_onResourceStop;

			API.onPlayerFinishedDownload += API_onPlayerFinishedDownload;

			API.onPlayerConnected += API_onPlayerConnected;
			API.onPlayerDisconnected += API_onPlayerDisconnected;

			API.onClientEventTrigger += API_onClientEventTrigger;

			API.onUpdate += API_onUpdate;
		}

		private void API_onResourceStart()
		{
			Instance = this;

			using (var reader = new StreamReader(File.OpenRead(API.getResourceFolder() + "\\props.txt"))) {
				while (!reader.EndOfStream) {
					var line = reader.ReadLine().Trim();
					if (line == "" || line.StartsWith("#")) {
						continue;
					}
					m_allowedProps.Add(line.ToLower());
				}
			}

			Prophunt.Log("Allowing {0} props.", m_allowedProps.Count);

			var players = API.getAllPlayers();
			foreach (var player in players) {
				player.freezePosition = false;

				var newPlayer = new PhPlayer(player);
				newPlayer.Seeker = BalanceShouldJoinSeekers();
				m_players.Add(newPlayer);
			}
		}

		private void API_onResourceStop()
		{
			foreach (var ply in m_players) {
				ply.CleanUp();
			}
		}

		private void API_onPlayerFinishedDownload(Client player)
		{
			API.consoleOutput("TODO: onPlayerFinishedDownload (but for now we use it for testing)");

			m_events.triggerClientEvent(player, "prophunt_metadata", API.toJson(m_metadata));

			if (m_gameState == PhGameState.Waiting && m_players.Count >= 2) {
				SetState(PhGameState.EndOfRound);
			}
		}

		private void API_onPlayerConnected(Client player)
		{
			API.consoleOutput("TODO: onPlayerConnected (but we use it)");

			var newPlayer = new PhPlayer(player);
			newPlayer.Seeker = BalanceShouldJoinSeekers();
			m_players.Add(newPlayer);
		}

		private void API_onPlayerDisconnected(Client player, string reason)
		{
			var ply = GetPlayer(player);
			ply.CleanUp();
			m_players.Remove(ply);
		}

		private void API_onClientEventTrigger(Client sender, string eventName, params object[] arguments)
		{
			if (eventName == "prophunt_rotation") {
				var player = GetPlayer(sender);
				var rotationZ = (float)arguments[0];

				if (player.Prop != null) {
					foreach (var ply in m_players) {
						if (ply == player || ply.Client.dimension != player.Client.dimension) {
							continue;
						}
						m_events.triggerClientEvent(ply.Client, "prophunt_rotation", player.Prop.handle, rotationZ);
					}
				}
			}
		}

		private void API_onUpdate()
		{
			if (m_gameState == PhGameState.Waiting) {
				return;
			}

			var tmStateDuration = DateTime.Now - m_gameStateChanged;

			if (m_gameState == PhGameState.Hiding) {
				if (tmStateDuration.TotalSeconds >= m_metadata.tmHiding) {
					SetState(PhGameState.Seeking);
				}

			} else if (m_gameState == PhGameState.Seeking) {
				if (tmStateDuration.TotalSeconds >= m_metadata.tmSeeking) {
					SetState(PhGameState.EndOfRound);
				}

			} else if (m_gameState == PhGameState.EndOfRound) {
				if (tmStateDuration.TotalSeconds >= m_metadata.tmEndOfRound) {
					if (m_players.Count > 1) {
						SetState(PhGameState.Hiding);
					} else {
						SetState(PhGameState.Waiting);
					}
				}
			}
		}

		private void SetState(PhGameState state)
		{
			Log("Setting game state: {0}", state);

			API.sendChatMessageToAll("Server state: ~r~" + m_gameState + "~s~ -> ~g~" + state.ToString());

			m_gameState = state;
			m_gameStateChanged = DateTime.Now;
			m_events.triggerClientEventForAll("prophunt_state", (int)state);

			if (m_gameState == PhGameState.Hiding) {
				foreach (var player in m_players) {
					player.CleanUp();
					player.Seeker = !player.Seeker;

                    //Here seekers can't see hiders but hiders can see seekers.
					if (player.Seeker) {
						player.Client.nametagVisible = true;
						player.Client.freezePosition = true;
					    player.Client.dimension = 0;
					} else {
						player.Client.nametagVisible = false;
						player.Client.freezePosition = false;
						player.Client.removeAllWeapons();
                        player.Client.dimension = 1;
                        GivePlayerRandomProp(player.Client);
					}
				}

			} else if (m_gameState == PhGameState.Seeking) {
				foreach (var player in m_players) {
					if (player.Seeker) {
						player.Client.freezePosition = false;
						player.Client.giveWeapon(WeaponHash.Grenade, 1, false, true);
						player.Client.giveWeapon(WeaponHash.SMG, 5000, true, true);
                        player.Client.dimension = 0; //Return back to dimension zero so he can search for hiders.
                    }
				}

			} else if (m_gameState == PhGameState.EndOfRound) {
				foreach (var player in m_players) {
					player.Client.nametagVisible = true;
				}

			}
		}

		private void EndGame()
		{
			m_gameState = PhGameState.Waiting;
			m_events.triggerClientEventForAll("prophunt_end");

			foreach (var player in m_players) {
				player.CleanUp();
				player.Client.freezePosition = false;
			}
		}

		private PhPlayer GetPlayer(Client player)
		{
			return (PhPlayer)player.getData("prophunt_player");
		}

		private void GivePlayerRandomProp(Client player)
		{
			var ply = GetPlayer(player);
			var propName = m_allowedProps[Rnd.Next(m_allowedProps.Count)];
			ply.SetProp(propName);
		}

		private void GiveAllPlayersRandomProps()
		{
			var players = API.getAllPlayers();
			foreach (var player in players) {
				GivePlayerRandomProp(player);
			}
		}

		private bool BalanceShouldJoinSeekers()
		{
			int numSeekers = 0;
			int numHiders = 0;
			for (int i = 0; i < m_players.Count; i++) {
				if (m_players[i].Seeker) {
					numSeekers++;
				} else {
					numHiders++;
				}
			}
			return (numSeekers < numHiders);
		}

		[Command("setprop")]
		public void SetPropCommand(Client sender, int num)
		{
			if (num < 0 || num >= m_allowedProps.Count) {
				sender.sendChatMessage("oops");
				return;
			}

			var player = (PhPlayer)sender.getData("prophunt_player");
			player.SetProp(m_allowedProps[num]);
		}

		[Command("begin")]
		public void BeginCommand(Client sender)
		{
			Log("Forcefully starting by /begin");
			SetState(PhGameState.EndOfRound);
		}

		[Command("state")]
		public void StateCommand(Client sender, PhGameState state)
		{
			Log("Forcefully setting state by /state");
			SetState(state);
		}

		[Command("end")]
		public void EndCommand(Client sender)
		{
			Log("Forcefully ending by /end");
			EndGame();
		}
	}
}
