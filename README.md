# Gil Transferer

A plugin to facilitate transfering gils within a single Service account.<br/>
Relies on TextAdvance, Lifestream, AutoRetainer and SimpleTweaks' `/estatelist` command. 

 # Todo
- Handle different destination types (Private, Apartment, FC), right now it will only go to FC room
- Find a better way to wait for territory change (instead of having to specify the territory id of indoors/outdoors)
- Correctly use Lifestream to change DC (I believe right now it will login to the character, then if it needs to change DC it will logout again and change)
- Remove unnecessary TaskQueue delays (Make processing faster overall)
- Make the process of setting up mannequins better, right now there are a few tasks failing when setting up, not all cases are covered. It only slows the process but doesn't seem to completely block/softlock/fail it.
- Make the process of setting up mannequins more automatic (TP to estate/room/apartment if not inside already)
- Move some of the settings from the Scenario settings to the Mannequin settings, for example the destination type, player for estate TP, room number (etc...), instead of being global to the scenario, should be per mannequin in case they are in separate places.
- Related to above task: have an option to also specify the estate ward/plot a mannequin is in so if 2 mannequins are in the same FC estate but different FC rooms, so that instead of TPing to estate/address again, it will just change room.
- Before setting up a mannequin, check if the retainer has space left (the 20 slots to sell) AND if the retainer is not gil capped (if gil capped, it will go to void, we dont want our precious money to vanish)
- Have an option to TP with lifestream address book instead.
- In the mannequin settings, when setting up the character assigned to a slot, be able to override the amount of gils to send.
- Make it so you process all assigned slots on a character before changing character.
- Add a check to verify that the slot you are purchasing is the right one (Read node text for price ?)
- Make it so that you can skip a character in queue if it fails somewhere, to continue processing other chars even if one fails.
- Add a skip current task button in case of stalling, for "manual debugging" ?
- Add a button to process the whole scenaro (Setup Mannequin, then process buyers)
- Add post processing on seller, buyer and all buyers complet (When character finishes buying, when seller finishes setting up mannequin or when all buyers have bought all slots: send a command, move somewhere ...)
- Add usage instructions
