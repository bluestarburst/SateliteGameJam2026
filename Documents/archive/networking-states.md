
### Voice Chat
- [ ] Lobby scene
    - [ ] Voice chat should always be enabled in lobby with push to talk (lobby id UI only)

- [ ] Ground Control
  - [ ] Ground control player only sends voice data when interacting with transmission console
  - [ ] Ground control player can only hear space player when interacting with transmission console
  - [ ] Ground control player can always hear other ground control players

- [ ] Space Scene
  - [ ] Space player sends voice data to ground control CONSOLE on push-to-talk
  - [ ] Space player can always hear ground control player
  - [ ] Space player can hear other space players within a certain radius


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



