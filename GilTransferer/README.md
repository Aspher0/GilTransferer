# Current Todo
- Handle apartments destination
- Make it possible to override the player for estate tp for each mannequin
- In the mannequin settings, when setting up the character assigned to a slot, be able to override the amount of gils to send.
- On a mannequin entry, be able to target a new mannequin and reassign the selected mannequin to the targetted one, updating every information associated.
- Make it so you process all assigned slots on a character before changing character.
- Add an error history to quickly view who didn't get processed and why, with a window to visualize them
- Be able to add a vector path to the estate outside/inside entrance and have the character follow the path with LifestreamIPC to get to the entrance. For example, if there's a wall in the middle, the user can set a path with multiple points (first point next to wall, then second point next to door for example) and lifestream will move to the last point by iterating through them.
- Add a "Process this mannequin" button to start buying this mannequin's assigned slots without having to start the whole scenario

# Future Todo
- Remove unnecessary TaskQueue delays during mannequin setup process (Make processing faster overall)
- Correctly use LifestreamIPC to change DC (I believe right now it will login to the character, then if it needs to change DC it will logout again and change)
- Remove YesAlready dependency by clicking "Yes" when entering the house, or let yesalready do it anyway if installed
- Click yes or no when it asks if you want to use an aetheryte ticket
- Make the process of setting up mannequins better, right now there are a few tasks failing when setting up, not all cases are covered. It only slows the process but doesn't seem to completely block/softlock/fail it.
- Add a check to verify that the slot you are purchasing is the right one (Read node text for price ?)
- Make the code cleaner
- Add usage instructions
