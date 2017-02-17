using GTANetworkServer;
using GTANetworkShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace prophunt
{
	public class PhPlayer
	{
		public Client Client { get; set; }
		public bool Spectating { get; set; }
		public bool Seeker { get; set; }
		public GTANetworkServer.Object Prop { get; set; }

		public PhPlayer(Client client)
		{
			Client = client;
			Seeker = false;

			Client.setData("prophunt_player", this);
		}

		public void CleanUp()
		{
			if (Prop != null) {
				Prop.delete();
				Prop = null;

				Client.resetSyncedData("prophunt_prophandle");
				Prophunt.Instance.m_events.triggerClientEvent(Client, "prophunt_removeprop");
			}
		}

		public bool SetProp(string prop)
		{
			if (!Prophunt.PropAllowed(prop)) {
				return false;
			}

			if (Prop != null) {
				Prop.delete();
			}
			Prop = API.shared.createObject(API.shared.getHashKey(prop), Client.position + new Vector3(0, 0, -0.975f), new Vector3());
			Prop.collisionless = true;
			Client.setSyncedData("prophunt_prophandle", Prop.handle);
			Prop.setSyncedData("player", Client);

			Prophunt.Instance.m_events.triggerClientEventForAll("prophunt_propset", Client.handle);

			return true;
		}
	}
}
