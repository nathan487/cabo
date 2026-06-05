[ProtoGateway] TickCoroutine loop started
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:361)
UnityEngine.MonoBehaviour:StartCoroutine (System.Collections.IEnumerator)
Cabo.Client.Network.ProtoGateway:StartTickIfNeeded () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:91)
Cabo.Client.Network.ProtoGateway:.ctor (string,int,UnityEngine.MonoBehaviour) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:80)
Cabo.Client.Runtime.ClientAppBootstrap:Awake () (at Assets/Scripts/ClientCore/Runtime/ClientAppBootstrap.cs:32)

[ProtoGateway] Tick coroutine started
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:StartTickIfNeeded () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:92)
Cabo.Client.Network.ProtoGateway:.ctor (string,int,UnityEngine.MonoBehaviour) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:80)
Cabo.Client.Runtime.ClientAppBootstrap:Awake () (at Assets/Scripts/ClientCore/Runtime/ClientAppBootstrap.cs:32)

[RoomClientController] Switched to real ProtoGateway
UnityEngine.Debug:Log (object)
Cabo.Client.Room.RoomClientController:SetProtoGateway (Cabo.Client.Network.ProtoGateway) (at Assets/Scripts/ClientCore/Room/RoomClientController.cs:50)
Cabo.Client.Runtime.ClientAppBootstrap:Awake () (at Assets/Scripts/ClientCore/Runtime/ClientAppBootstrap.cs:33)

[ClientAppBootstrap] ProtoGateway injected -> 127.0.0.1:8888
UnityEngine.Debug:Log (object)
Cabo.Client.Runtime.ClientAppBootstrap:Awake () (at Assets/Scripts/ClientCore/Runtime/ClientAppBootstrap.cs:34)

[FontHelper] Using Resources font: ZCOOLKuaiLe
UnityEngine.Debug:Log (object)
FontHelper:GetChineseFont () (at Assets/Scripts/Game/FontHelper.cs:27)
Cabo.Client.UI.LobbyRoomDemoUI:ResolveFont () (at Assets/Scripts/ClientCore/UI/LobbyRoomDemoUI.cs:321)
Cabo.Client.UI.LobbyRoomDemoUI:BuildUi () (at Assets/Scripts/ClientCore/UI/LobbyRoomDemoUI.cs:67)
Cabo.Client.UI.LobbyRoomDemoUI:Awake () (at Assets/Scripts/ClientCore/UI/LobbyRoomDemoUI.cs:31)
UnityEngine.GameObject:AddComponent<Cabo.Client.UI.LobbyRoomDemoUI> ()
Cabo.Client.Runtime.ClientAppBootstrap:Awake () (at Assets/Scripts/ClientCore/Runtime/ClientAppBootstrap.cs:45)

[LobbyUI] Connect button clicked
UnityEngine.Debug:Log (object)
Cabo.Client.UI.LobbyRoomDemoUI:OnConnectClick () (at Assets/Scripts/ClientCore/UI/LobbyRoomDemoUI.cs:134)
UnityEngine.EventSystems.EventSystem:Update () (at ./Library/PackageCache/com.unity.ugui@1.0.0/Runtime/EventSystem/EventSystem.cs:530)

[TcpNetworkClient] Connected to 127.0.0.1:8888
UnityEngine.Debug:Log (object)
Cabo.Client.Network.TcpNetworkClient/<ConnectAsync>d__23:MoveNext () (at Assets/Scripts/Network/TcpNetworkClient.cs:59)
UnityEngine.UnitySynchronizationContext:ExecuteTasks ()

[LobbyUI] Create Room button clicked
UnityEngine.Debug:Log (object)
Cabo.Client.UI.LobbyRoomDemoUI:OnCreateRoomClick () (at Assets/Scripts/ClientCore/UI/LobbyRoomDemoUI.cs:140)
UnityEngine.EventSystems.EventSystem:Update () (at ./Library/PackageCache/com.unity.ugui@1.0.0/Runtime/EventSystem/EventSystem.cs:530)

[ProtoGateway] CreateRoom called (nickname=PlayerA, maxPlayers=4)
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:CreateRoom (int) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:152)
Cabo.Client.Room.RoomClientController:CreateRoom (int) (at Assets/Scripts/ClientCore/Room/RoomClientController.cs:100)
Cabo.Client.UI.LobbyRoomDemoUI:OnCreateRoomClick () (at Assets/Scripts/ClientCore/UI/LobbyRoomDemoUI.cs:141)
UnityEngine.EventSystems.EventSystem:Update () (at ./Library/PackageCache/com.unity.ugui@1.0.0/Runtime/EventSystem/EventSystem.cs:530)

[ProtoGateway] Received 83 bytes from server (queued)
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnDataReceived (byte[]) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:344)
Cabo.Client.Network.TcpNetworkClient/<ReceiveLoop>d__26:MoveNext () (at Assets/Scripts/Network/TcpNetworkClient.cs:120)
System.Threading._ThreadPoolWaitCallback:PerformWaitCallback ()

[ProtoGateway] Processing 1 queued messages on main thread
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:384)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] OnCreateRoomRsp called — code=0
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnCreateRoomRsp (Game.Room.CreateRoomRsp) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:398)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.CreateRoomRsp> (Game.Room.CreateRoomRsp) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:56)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] CreateRoom request 1 resolved
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway/<>c__DisplayClass89_0:<SendWithTracking>b__0 () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:312)
Cabo.Client.Network.RequestTracker:Resolve (long) (at Assets/Scripts/Network/RequestTracker.cs:48)
Cabo.Client.Network.ProtoGateway:OnCreateRoomRsp (Game.Room.CreateRoomRsp) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:403)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.CreateRoomRsp> (Game.Room.CreateRoomRsp) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:56)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Room created: code=9CY6V7, playerId=10000
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnCreateRoomRsp (Game.Room.CreateRoomRsp) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:404)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.CreateRoomRsp> (Game.Room.CreateRoomRsp) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:56)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] OnRoomStateNotify called — room=9CY6V7
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnRoomStateNotify (Game.Room.RoomStateNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:409)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStateNotify> (Game.Room.RoomStateNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:74)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Firing RoomUpdated: code=9CY6V7, players=1
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnRoomStateNotify (Game.Room.RoomStateNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:414)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStateNotify> (Game.Room.RoomStateNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:74)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[RoomClientController] Room updated: code=9CY6V7, players=1
UnityEngine.Debug:Log (object)
Cabo.Client.Room.RoomClientController:OnRoomUpdated (Cabo.Client.Network.RoomSnapshot) (at Assets/Scripts/ClientCore/Room/RoomClientController.cs:175)
Cabo.Client.Network.ProtoGateway:OnRoomStateNotify (Game.Room.RoomStateNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:415)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStateNotify> (Game.Room.RoomStateNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:74)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Received 60 bytes from server (queued)
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnDataReceived (byte[]) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:344)
Cabo.Client.Network.TcpNetworkClient/<ReceiveLoop>d__26:MoveNext () (at Assets/Scripts/Network/TcpNetworkClient.cs:120)
System.Threading._ThreadPoolWaitCallback:PerformWaitCallback ()

[ProtoGateway] Processing 1 queued messages on main thread
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:384)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] SetReady request 2 resolved
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway/<>c__DisplayClass89_0:<SendWithTracking>b__0 () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:312)
Cabo.Client.Network.RequestTracker:Resolve (long) (at Assets/Scripts/Network/RequestTracker.cs:48)
Cabo.Client.Network.ProtoGateway:OnReadyRsp (Game.Room.ReadyRsp) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:439)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.ReadyRsp> (Game.Room.ReadyRsp) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:65)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] OnRoomStateNotify called — room=9CY6V7
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnRoomStateNotify (Game.Room.RoomStateNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:409)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStateNotify> (Game.Room.RoomStateNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:74)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Firing RoomUpdated: code=9CY6V7, players=1
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnRoomStateNotify (Game.Room.RoomStateNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:414)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStateNotify> (Game.Room.RoomStateNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:74)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[RoomClientController] Room updated: code=9CY6V7, players=1
UnityEngine.Debug:Log (object)
Cabo.Client.Room.RoomClientController:OnRoomUpdated (Cabo.Client.Network.RoomSnapshot) (at Assets/Scripts/ClientCore/Room/RoomClientController.cs:175)
Cabo.Client.Network.ProtoGateway:OnRoomStateNotify (Game.Room.RoomStateNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:415)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStateNotify> (Game.Room.RoomStateNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:74)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Received 93 bytes from server (queued)
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnDataReceived (byte[]) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:344)
Cabo.Client.Network.TcpNetworkClient/<ReceiveLoop>d__26:MoveNext () (at Assets/Scripts/Network/TcpNetworkClient.cs:120)
System.Threading._ThreadPoolWaitCallback:PerformWaitCallback ()

[ProtoGateway] Received 68 bytes from server (queued)
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnDataReceived (byte[]) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:344)
Cabo.Client.Network.TcpNetworkClient/<ReceiveLoop>d__26:MoveNext () (at Assets/Scripts/Network/TcpNetworkClient.cs:120)
System.Threading._ThreadPoolWaitCallback:PerformWaitCallback ()

[ProtoGateway] Processing 2 queued messages on main thread
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:384)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Player joined: 10001
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnPlayerJoinNotify (Game.Room.PlayerJoinNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:450)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.PlayerJoinNotify> (Game.Room.PlayerJoinNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:77)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] OnRoomStateNotify called — room=9CY6V7
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnRoomStateNotify (Game.Room.RoomStateNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:409)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStateNotify> (Game.Room.RoomStateNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:74)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Firing RoomUpdated: code=9CY6V7, players=2
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnRoomStateNotify (Game.Room.RoomStateNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:414)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStateNotify> (Game.Room.RoomStateNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:74)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[RoomClientController] Room updated: code=9CY6V7, players=2
UnityEngine.Debug:Log (object)
Cabo.Client.Room.RoomClientController:OnRoomUpdated (Cabo.Client.Network.RoomSnapshot) (at Assets/Scripts/ClientCore/Room/RoomClientController.cs:175)
Cabo.Client.Network.ProtoGateway:OnRoomStateNotify (Game.Room.RoomStateNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:415)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStateNotify> (Game.Room.RoomStateNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:74)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] OnRoomStateNotify called — room=9CY6V7
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnRoomStateNotify (Game.Room.RoomStateNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:409)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStateNotify> (Game.Room.RoomStateNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:74)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Firing RoomUpdated: code=9CY6V7, players=2
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnRoomStateNotify (Game.Room.RoomStateNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:414)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStateNotify> (Game.Room.RoomStateNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:74)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[RoomClientController] Room updated: code=9CY6V7, players=2
UnityEngine.Debug:Log (object)
Cabo.Client.Room.RoomClientController:OnRoomUpdated (Cabo.Client.Network.RoomSnapshot) (at Assets/Scripts/ClientCore/Room/RoomClientController.cs:175)
Cabo.Client.Network.ProtoGateway:OnRoomStateNotify (Game.Room.RoomStateNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:415)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStateNotify> (Game.Room.RoomStateNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:74)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Received 19 bytes from server (queued)
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnDataReceived (byte[]) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:344)
Cabo.Client.Network.TcpNetworkClient/<ReceiveLoop>d__26:MoveNext () (at Assets/Scripts/Network/TcpNetworkClient.cs:120)
System.Threading._ThreadPoolWaitCallback:PerformWaitCallback ()

[ProtoGateway] Received 96 bytes from server (queued)
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnDataReceived (byte[]) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:344)
Cabo.Client.Network.TcpNetworkClient/<ReceiveLoop>d__26:MoveNext () (at Assets/Scripts/Network/TcpNetworkClient.cs:120)
System.Threading._ThreadPoolWaitCallback:PerformWaitCallback ()

[ProtoGateway] Received 34 bytes from server (queued)
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnDataReceived (byte[]) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:344)
Cabo.Client.Network.TcpNetworkClient/<ReceiveLoop>d__26:MoveNext () (at Assets/Scripts/Network/TcpNetworkClient.cs:120)
System.Threading._ThreadPoolWaitCallback:PerformWaitCallback ()

[ProtoGateway] Processing 3 queued messages on main thread
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:384)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] StartGame request 3 resolved
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway/<>c__DisplayClass89_0:<SendWithTracking>b__0 () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:312)
Cabo.Client.Network.RequestTracker:Resolve (long) (at Assets/Scripts/Network/RequestTracker.cs:48)
Cabo.Client.Network.ProtoGateway:OnStartGameRsp (Game.Room.StartGameRsp) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:445)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.StartGameRsp> (Game.Room.StartGameRsp) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:68)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Room 1 started!
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnRoomStartNotify (Game.Room.RoomStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:465)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Room.RoomStartNotify> (Game.Room.RoomStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:86)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] ========== GameStartNotify Received ==========
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnGameStartNotify (Game.Game.GameStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:473)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.GameStartNotify> (Game.Game.GameStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:91)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Round: 1, FirstPlayer: 10000
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnGameStartNotify (Game.Game.GameStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:474)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.GameStartNotify> (Game.Game.GameStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:91)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] YourView: NOT NULL
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnGameStartNotify (Game.Game.GameStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:475)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.GameStartNotify> (Game.Game.GameStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:91)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Current PendingGameStart: NULL
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnGameStartNotify (Game.Game.GameStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:476)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.GameStartNotify> (Game.Game.GameStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:91)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] ✅ PendingGameStart set successfully
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnGameStartNotify (Game.Game.GameStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:490)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.GameStartNotify> (Game.Game.GameStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:91)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] ✅ Verify: PendingGameStart is now NOT NULL
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnGameStartNotify (Game.Game.GameStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:491)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.GameStartNotify> (Game.Game.GameStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:91)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Starting LoadGameSceneNextFrame coroutine...
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnGameStartNotify (Game.Game.GameStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:496)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.GameStartNotify> (Game.Game.GameStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:91)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] LoadGameSceneNextFrame: waiting one frame...
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway/<LoadGameSceneNextFrame>d__108:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:513)
UnityEngine.MonoBehaviour:StartCoroutine (System.Collections.IEnumerator)
Cabo.Client.Network.ProtoGateway:OnGameStartNotify (Game.Game.GameStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:497)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.GameStartNotify> (Game.Game.GameStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:91)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] ================================================
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnGameStartNotify (Game.Game.GameStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:504)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.GameStartNotify> (Game.Game.GameStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:91)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] ========== TurnStartNotify Received ==========
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnTurnStartNotify (Game.Game.TurnStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:522)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.TurnStartNotify> (Game.Game.TurnStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:94)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Room: 1
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnTurnStartNotify (Game.Game.TurnStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:523)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.TurnStartNotify> (Game.Game.TurnStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:94)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Current Player: 10000
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnTurnStartNotify (Game.Game.TurnStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:524)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.TurnStartNotify> (Game.Game.TurnStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:94)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Turn: 1
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnTurnStartNotify (Game.Game.TurnStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:525)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.TurnStartNotify> (Game.Game.TurnStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:94)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Round: 1
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnTurnStartNotify (Game.Game.TurnStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:526)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.TurnStartNotify> (Game.Game.TurnStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:94)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] Local Player ID: '10000'
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnTurnStartNotify (Game.Game.TurnStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:527)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.TurnStartNotify> (Game.Game.TurnStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:94)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] OnTurnStart subscribers: 0
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnTurnStartNotify (Game.Game.TurnStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:528)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.TurnStartNotify> (Game.Game.TurnStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:94)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] ⚠️ No OnTurnStart subscribers yet, caching notification for later delivery
UnityEngine.Debug:LogWarning (object)
Cabo.Client.Network.ProtoGateway:OnTurnStartNotify (Game.Game.TurnStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:537)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.TurnStartNotify> (Game.Game.TurnStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:94)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] ================================================
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway:OnTurnStartNotify (Game.Game.TurnStartNotify) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:545)
Cabo.Client.Network.MessageDispatcher:InvokeHandler<Game.Game.TurnStartNotify> (Game.Game.TurnStartNotify) (at Assets/Scripts/Network/MessageDispatcher.cs:153)
Cabo.Client.Network.MessageDispatcher:Dispatch (Game.Messages.ServerMessage) (at Assets/Scripts/Network/MessageDispatcher.cs:94)
Cabo.Client.Network.ProtoGateway:<ProcessIncomingQueue>b__96_0 (Game.Messages.ServerMessage) (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:389)
Cabo.Client.Network.MessageCodec:FeedBytes (byte[],System.Action`1<Game.Messages.ServerMessage>) (at Assets/Scripts/Network/MessageCodec.cs:54)
Cabo.Client.Network.ProtoGateway:ProcessIncomingQueue () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:387)
Cabo.Client.Network.ProtoGateway/<TickCoroutine>d__95:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:367)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] LoadGameSceneNextFrame: about to load scene. PendingGameStart is NOT NULL
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway/<LoadGameSceneNextFrame>d__108:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:515)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[ProtoGateway] LoadGameSceneNextFrame: GameSceneBootstrap.LoadGameScene() called
UnityEngine.Debug:Log (object)
Cabo.Client.Network.ProtoGateway/<LoadGameSceneNextFrame>d__108:MoveNext () (at Assets/Scripts/ClientCore/Network/ProtoGateway.cs:517)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[GameSceneController] ✅ Singleton instance set on 'GameSceneController'
UnityEngine.Debug:Log (object)
Cabo.Client.Game.GameSceneController:Awake () (at Assets/Scripts/ClientCore/Game/GameSceneController.cs:30)

[GameSceneController] ⚠️ Duplicate instance detected on 'GameSceneController'. Destroying to prevent conflicts.
UnityEngine.Debug:LogWarning (object)
Cabo.Client.Game.GameSceneController:Awake () (at Assets/Scripts/ClientCore/Game/GameSceneController.cs:23)

[GameSceneController] ⚠️ Existing instance: 'GameSceneController', Current instance: 'GameSceneController'
UnityEngine.Debug:LogWarning (object)
Cabo.Client.Game.GameSceneController:Awake () (at Assets/Scripts/ClientCore/Game/GameSceneController.cs:24)

[UIToolkitTester] Tester is DISABLED - use only for standalone UI testing
UnityEngine.Debug:Log (object)
Cabo.Client.Game.GameTableUIToolkitTester:Start () (at Assets/Scripts/ClientCore/Game/GameTableUIToolkitTester.cs:18)

