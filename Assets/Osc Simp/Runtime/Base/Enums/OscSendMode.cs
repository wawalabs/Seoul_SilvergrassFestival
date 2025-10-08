/*
	Created by Carl Emil Carlsen.
	Copyright 2016-2023 Sixth Sensor.
	All rights reserved.
	http://sixthsensor.dk
*/


namespace OscSimpl
{
	/// <summary>
	/// Enum representing the mode of transmission for OscOut.
	/// Can either be UnicastToSelf, Unicast, Broadcast or Multicast.
	/// </summary>
	public enum OscSendMode { UnicastToSelf, Unicast, Broadcast, Multicast }
}