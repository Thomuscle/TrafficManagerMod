## Repository Description
The repository contains all of the code for the existing mod, Traffic Manager: President Edition as well as the code added to the mod to implement custom traffic light algorithms. All code pertaining to custom traffic light algorithms can be found in TLM/TLM.
### Classes added on top of Traffic Manager: President Edition
 - **API/APIget.cs:** Provides functions for retrieving live phase and vehicle data at intersections as well as traffic light algorithms which can be called from inside TrafficLight/FlexibleTrafficLights.cs.
 - **API/NodeRecordingObject.cs:** This is an object that stores recorded data about an intersection for out put inot a csv file.
 - **API/Phase.cs:** This is an object which stores one combination of movements at an intersection.
 - **TrafficLight/FlexibleTrafficLights.cs:** Traffic light class which contains functions for controlling a traffic light within the city.
 - **TrafficLight/FlexibleTrafficLightsStep.cs:** This is a phase in a form that a flexible traffic light can interpret and run at an intersection with traffic lights.
 - **UI/MainMenu/AlgorithmPanel.cs:** This is a UI panel with four algorithms that can be toggled and a button for recording data.

### Key classes in Traffic Manager: President Edition that had to be changed
 - Geometry/NodeGeometry.cs
 - Geometry/SegmentGeometry.cs
 - Manager/SegmentEndManager.cs
 - Manager/TrafficLightSimulationManager.cs
 - Traffic/SegmentEnd.cs
 - Traffic/VehicleState.cs
 - TrafficLight/CustomSegmentLight.cs
 - TrafficLight/TrafficLightSimulation.cs
 - UI/UIBase.cs
 
### Poster
There is a poster found within the compendium which briefly covers the aim of the project; the results of the project; the conclusions made about Cities: Skylines as a simulation tool and the conclusions made about the effectiveness of the implemented algorithms; and potential future work for Cities: Skylines.

### Recorded Data 
Within the compendium all of the raw data used to generate the graphs in the report and poster can be found.
- AWAITS.csv
- AWAITS++.csv
- ROUND_ROBIN.csv
- overall graphs.xlsx




