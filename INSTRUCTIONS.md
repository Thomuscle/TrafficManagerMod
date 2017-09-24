## Setting up a development environment
- Install Steam (http://store.steampowered.com/about/).
- Once Steam is installed, use Steam to purchase Cities: Skylines.
- Install Cities: Skylines through Steam.
- Open Cities: Skylines.
- Once the Main menu for Cities: Skylines has loaded, select Steam Workshop.
- In the Steam overlay that appears, enter in the search bar: slowspeed.
- Click the green '+' that appears when you hover over the search result called Slow Speed by Scott.
- Then type in the search bar: sky tower.
- Click the green '+' that appears when you hover over the search result with 4 stars called Sky Tower by Krodge.
- Exit the overlay and then the game.
- Install Visual Studio with .NET framework 4.0 or above (https://www.visualstudio.com/).
- Install git (https://git-scm.com/downloads).
- Use git cmd to navigate to the folder where you would like the project to be saved - we will call this folder "X" for the instructions.
- Enter in the git cmd: git clone https://github.com/Thomuscle/TrafficManagerMod.git.
- Open X/AucklandCitySaveFile and copy the file named: Auckland v0%002E1 local.crp.
- Navigate to C:\Users\"your pc name"\AppData\Local\Colossal Order\Cities_Skylines\Saves and paste.
- Open Visual Studio.
- In Visual Studio open TMPE.sln, this should be found in X\TLM.
- Once the project is open there might be a problem - navigate to X/TLM/TLM/API/APIget.cs in Visual Studio's solution explorer which should be on the right. If there are no compilation errors go to the next step. Otherwise,  right click the folder named API in solution explorer and add existing item, navigate to TLM/TLM/API in the window that pops up and select Phase.cs and NodeRecordingObject.cs to add.

## Building and running
- In the top panel of Visual Studio there is a Build tab, press this and select build solution.
- The code should compile - dont worry about the warnings and if this error appears don't worry: "Error	CS0246	The type or namespace name 'Util' could not be found (are you missing a using directive or an assembly reference?)"	.
- Now within your project folder navigate to TLM/TLM/bin/release and copy TrafficManager.dll.
- Navigate to C:\Users\"your pc name"\AppData\Local\Colossal Order\Cities_Skylines\Addons\Mods.
- Create a folder here called TrafficManager and paste TrafficManager.dll into this folder.
- Then run Cities: Skylines from Steam.
- Select Content Manager.
- Select Mods.
- Enable Traffic Manager: President Edition and Slowspeed.
- Return to the main menu.
- Select Load Game.
- Select the Auckland City save file and load it.
- The Auckland City map should load up with the mod enabled.

## Using the mod
- Click the crown at the top of the screen.
- A panel with 5 buttons should appear (4 toggle buttons and a record button).
- If you press AWAITS, AWAITS++ or Round-Robin it will activate that traffic light algorithm throughout the city.
- If you want to switch to a different traffic light algoithm you first have to disable the previous algorithm selected, this is done by clicking its button again.
- Once the previous algorithm is disabled you can select from AWAITS, AWAITS++ or Round-Robin once again.
- While any of these algorithms are running you can press the record button, this while start counting up in seconds.
- Once you press stop recording the counter will reset and all the traffic data recorded in that time will be saved to a csv file named data.csv on the desktop.

## Potential Future Work
### User Interface
- The Algorithm Panel UI could be improved to look nicer.
- The Algorithm panel could also automatically disable an already active algorithm when you select another algorithm rather than having to deselect it manually.
- Could add some UI when you select an intersection to display information about the algorithm running at the intersection - an example could be a counter for the time its been in the current phase.

### New Algorithms
There is also a button called toggle MyATCS which currently does nothing but is set up so that a new traffic light algorithm can be written and implemented here. All that needs to be done is finish the function in APIget called getNextIndexMyATCS which determines the next phase at an intersection. 

### Calibration
Could implement more functionality in the record button so that traffic data can be exported into tools like Aimsun to see how Cities: Skylines  compares to a more trusted simulation tool.




