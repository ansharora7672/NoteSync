**NoteSync**
Collaborative Note-Taking Desktop Application

**Overview**
NoteSync is a powerful desktop application developed using C# and WPF within the .NET framework. It transforms traditional note-taking into a dynamic, interactive experience, ideal for team collaboration, online teaching, and creative projects.

Features
**Client Features**
1. User Authentication: Users can sign up and log in.
2. Notebook Management: Create and manage multiple notebooks.
3. Canvas Creation: Create new canvases within notebooks.
4. Real-Time Collaboration: Add multiple collaborators to a canvas for real-time collaboration.
5. Save and Share: Save canvases and share them using a generated code/link.
   
**Server Features**
1. User Authentication: Authenticate users and manage sessions.
2. Collaboration Management: Allow collaborators to join sessions via code/link.
3. Real-Time Communication: Facilitate real-time communication between clients.
4. Data Synchronization: Ensure synchronization of canvas content across clients.
5. Logging and Monitoring: Maintain logs of all communications and monitor multiple sessions.
   
**Technical Details**
1. Communication Protocol
2. Protocol: WebSocket over TCP for real-time communication.
3. Message Format: JSON format for message serialization.
4. Message Types: Includes DrawCommand, EraseCommand, AddText, RemoveText, PasteImage, DeleteImage, AddCollaborator, JoinRequest, CanvasUpdate, SyncCommand.

