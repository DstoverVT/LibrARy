# LibrARy

Unity Project for research at Virginia Tech's 3D Interaction Group under Dr. Doug Bowman. This research was conducted as part of this lab while completing my Computer Engineering Master's Degree.
This project used Microsoft's MRTK (Mixed Reality ToolKit) and was deployed to the Microsoft HoloLens 2. It was used to run a user study with 20 participants at Virginia Tech's library to collect data on how user's 
configure Augmented Reality (AR) apps while performing tasks in the library, such as finding 2 library books.

This app provided a unique UI and experience to make adaptations to apps, such as moving, scaling, changing transparency, and changing frame of reference (head, body, world fixed). It displays information relevant to the user's library tasks, 
and collected data from the HoloLens 2 such as a video+audio recording, eye gaze data and depth sensor images, using the Microsoft [HoloLens2ForCV](https://github.com/microsoft/HoloLens2ForCV)
repository to record raw sensor streams, as well as [another repository](https://github.com/petergu684/HoloLens2-ResearchMode-Unity/tree/master) to use HoloLens2ForCV in Unity. Additionally, a novel method to have apps be "body-tracked" to
the user was implemented based on the user's current walking speed and direction vector. 

A research paper to analyze the data from the study and determine research conclusions is in works and will be published at some point.

Here is an example of the app in use:
![library_image](https://github.com/DstoverVT/LibrARy/assets/53241758/82517679-b10e-4651-a85b-1a5e08a4ef95)
