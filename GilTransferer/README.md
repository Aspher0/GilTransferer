# Gil Transferer

A plugin to facilitate transfering gils between alts within a single Service account.<br/>
Relies on TextAdvance, YesAlready, Lifestream and AutoRetainer.

# Current Todo
- Remove unnecessary TaskQueue delays (Make processing faster overall)
- Correctly use Lifestream to change DC (I believe right now it will login to the character, then if it needs to change DC it will logout again and change)
- Move some of the settings from the Scenario settings to the Mannequin settings, for example the destination type, player for estate TP, room number (etc...), instead of being global to the scenario, should be per mannequin in case they are in separate places.
- Make it so that every scenario setting can be overriden in mannequins
- Related to above task: have an option to also specify the estate ward/plot a mannequin is in so if 2 mannequins are in the same FC estate but different FC rooms, so that instead of TPing to estate/address again, it will just change room.
- Add "IsJumping" to the conditions in the task queue, so that it will wait for the character to finish jumping before doing the next action.
- In the mannequin settings, when setting up the character assigned to a slot, be able to override the amount of gils to send.
- Make it so you process all assigned slots on a character before changing character.
- Make it so you can pause the queue, resume it, skip current task, skip current character, etc... to have more control over the process.
- If the plugin cannot open the friend's Estate list, skip the char
- Add an error history to quickly view who didn't get processed and why

# Future Todo
- Remove YesAlready dependency by clicking "Yes" when entering the house, or let yesalready do it anyway if installed
- Click yes or no when it asks if you want to use an aetheryte ticket
- Handle different destination types (Private, Apartment, FC), right now it will only go to FC room
- Find a better way to wait for territory change (instead of having to specify the territory id of indoors/outdoors)
- Make the process of setting up mannequins more automatic (TP to estate/room/apartment if not inside already)
- Make the process of setting up mannequins better, right now there are a few tasks failing when setting up, not all cases are covered. It only slows the process but doesn't seem to completely block/softlock/fail it.
- Before setting up a mannequin, check if the retainer has space left (the 20 slots to sell) AND if the retainer is not gil capped (if gil capped, it will go to void, we dont want our precious money to vanish)
- Have an option to TP with lifestream address book instead.
- Add post processing on seller, buyer, autoretainer post process and all buyers complet (When a character finishes buying, when the seller finishes setting up mannequin, when all buyers have bought all slots or on autoretainer post process when you finish sending submarines etc: send a command, move somewhere, do something ...)
- Add a check to verify that the slot you are purchasing is the right one (Read node text for price ?)
- Add usage instructions
