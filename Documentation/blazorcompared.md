# Blazor WASM compared versus Typescript UI Frameworks
This article tries to compare implementing single page web apps with Blazor WASM versus a Typescript based UI framework like Angular
## Advantages
* Familiar for .NET developers
* Have shared types in a shared library for both front and backend without being forced to use Node as your backend
## Disadvantages
* Much larger transfer sizes (most language features for Javascript are built into browsers, vs having to transfer .NET libraries for Blazor WASM)
* Pretty much no native support for Browser Api features
* Not as widely adopted (Less documentation, tutorials, reference implementations, libraries, ...)
* Debugging of client side code does not work reliably