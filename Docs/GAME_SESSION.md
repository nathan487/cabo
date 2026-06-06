# Game Session Capture

> Recording a full 4-player game session for reference.
> Use this as test data for Unity client development.

## Setup

```bash
# Terminal 1: Server
cd MuduoBaseGameServer/build
./GameServer 8888

# Terminals 2-5: 4 CLI clients
cd MuduoBaseGameServer/cli_client/build
./cabo_cli_client
```

## Server Log

```
[PASTE SERVER LOG HERE]
```

## Client 1 (pl1, Host)

```
[PASTE CLIENT 1 LOG HERE]
```

## Client 2 (pl2)

```
[PASTE CLIENT 2 LOG HERE]
```

## Client 3 (pl3)

```
[PASTE CLIENT 3 LOG HERE]
```

## Client 4 (pl4)

```
[PASTE CLIENT 4 LOG HERE]
```

## Key Events to Cover

- [ ] Room creation and join
- [ ] Ready + Game start
- [ ] Draw from deck
- [ ] Discard drawn card
- [ ] Replace with drawn card (single + multi)
- [ ] Take from discard pile (single + multi)
- [ ] Peek Self skill (7-8)
- [ ] Spy skill (9-10)
- [ ] Swap skill (11-12)
- [ ] Discard skill card without using skill
- [ ] Call CABO
- [ ] Final round turns
- [ ] Round reveal + scoring
- [ ] Inter-round ready/start
- [ ] Multi-round play
- [ ] Game over + rankings
