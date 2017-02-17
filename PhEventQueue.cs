using GTANetworkServer;
using GTANetworkShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace prophunt
{
	public class PhEventQueue
	{
		private API m_api;
		private List<Client> m_readyClients = new List<Client>();
		private List<Tuple<Client, string, object[]>> m_queue = new List<Tuple<Client, string, object[]>>();

		public PhEventQueue(API api)
		{
			m_api = api;
			m_api.onPlayerFinishedDownload += api_onPlayerFinishedDownload;
		}

		private void api_onPlayerFinishedDownload(Client player)
		{
			for (var i = 0; i < m_queue.Count; i++) {
				var item = m_queue[i];

				if (item.Item1.handle.Value != player.handle.Value) {
					continue;
				}

				m_api.triggerClientEvent(player, item.Item2, item.Item3);
				m_queue.RemoveAt(i--);
			}

			m_readyClients.Add(player);
		}

		public void triggerClientEventForAll(string name, params object[] args)
		{
			var players = m_api.getAllPlayers();
			foreach (var player in players) {
				triggerClientEvent(player, name, args);
			}
		}

		public void triggerClientEvent(Client player, string name, params object[] args)
		{
			if (m_readyClients.Find((p) => p.handle.Value == player.handle.Value) != null) {
				m_api.triggerClientEvent(player, name, args);
				return;
			}
			m_queue.Add(new Tuple<Client, string, object[]>(player, name, args));
		}
	}
}
