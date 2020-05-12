using System;
using System.Runtime.Remoting.Metadata.W3cXsd2001;

namespace Allsetra.Prototypes.EventHubs.ThroughputTest.Models
{
	public class Message
	{
		public byte[] Content { get; set; }
		public string Id { get; set; }
		public string Name { get; set; }
		public string Ip { get; set; }
		public DateTime Time { get; set; }

		private Message SetContent( byte[] content )
		{
			Content = content;
			return this;
		}

		public static Message GetSimpleMessage(string name = null)
		{
			// Should be about 55 bytes if name is null.
			return new Message
			{
				// 36 bytes
				Id = Guid.NewGuid().ToString(),
				// 0-inf bytes
				Name = name,
				// 11 bytes
				Ip = "192.168.1.0",
				// 8 bytes
				Time = DateTime.Now,
			};
		}

		public static Message GetFixedData(string name = null)
		{
			return GetSimpleMessage( name ).
				SetContent( SoapHexBinary.
							   Parse( "000000000000007308010000016EB60A3A900002DFB72A1EBED43A0004005213001C00190B010102000300B300B4014503F0011502511E52405900074237FC180018430FB854002E55075F7300005A000006C700017C83F100004FB81007A350E55300017DDF5707A145E464000000A4014E00000000000000000100007780" ).
							   Value );
		}

		public static Message GetRandomData(string name = null, int minBytes = 256, int maxBytes = 2048)
		{
			throw new NotImplementedException();
		}
	}
}
