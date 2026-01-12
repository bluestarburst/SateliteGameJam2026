
### Voice Chat
- [x] Lobby scene
    - [x] Voice chat AUTOMATICALLY enabled for all lobby members (no PTT, always on)
    - [x] Do NOT spawn remote player prefabs in lobby
    - [x] Use lightweight voice proxies (AudioSource only) for voice playback

- [x] Ground Control
  - [x] Ground control player AUTOMATICALLY sends voice to other Ground Control players (always on, no PTT)
  - [x] Ground control player sends voice to ALL Space Station players ONLY when tethered to transmission console
  - [x] Ground control player can only hear Space Station players when at transmission console
  - [x] Ground control player can always hear other Ground Control players

- [x] Space Scene
  - [x] Space player AUTOMATICALLY sends voice to nearby Space Station players (proximity-based, always on)
  - [x] Space player sends voice to Ground Control ONLY when pressing 'B' (push-to-talk)
  - [x] Space player can always hear Ground Control player
  - [x] Space player can hear other Space Station players within a certain radius


### Satellite Status Sync
- [ ] Percent health
- [ ] Indicators for damaged components (wires, screws, dirty solar panels, etc)
- [ ] Position and rotation of satellite parts (e.g. rotating solar panels)


### Misc items Sync
- [ ] Position and rotation of movable objects (e.g. tools, boxes)
- [ ] State of interactable objects (e.g. switches, buttons, levers)
- [ ] State of consoles (e.g. which console is active, current screen)

Ideas:
Physics already syncs

### Security Camera Feeds (optional)
- [ ] Ground control players can view space camera feeds on monitors in the ground control room
- [ ] Space players can view ground control camera feeds on monitors in the space station

 In order to create 'realistic' security camera feeds, we will need to set up additional cameras and create a dummy version of the other player's scene. This probably won't be performance friendly, so we may need to limit the number of camera feeds that can be viewed at once.

 Ideas:
 - Maybe make the satellite and ground control room be in the same scene, but separated by a large distance, and use layers to control what each player can see. This way we can have security cameras that just render the other player's view without needing extra cameras.
 - Limit the number of security camera feeds that can be viewed at once to reduce performance impact.



