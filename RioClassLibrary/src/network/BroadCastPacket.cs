namespace rbcl.network;

public readonly struct BroadCastPacket
{
	public string Data { get; }

	public int SenderHash { get; }

	public BroadCastPacket (string data, int senderHash)
	{
		Data = data;
		SenderHash = senderHash;
	}
}