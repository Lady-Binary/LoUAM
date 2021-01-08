# LoU Auto-Map

Discord server: https://discord.gg/RWAqYcV

![LoUAM Screenshot](Screenshot.png?raw=true "LoUAM Screenshot")

## So what is LoU Auto-Map?

LoU Auto-Map is a totally free tool inspired by UO Auto-Map, the popular premiere mapping tool for Ultima Online. 

## Why LoUAM and not LoAAM?

For one simple reason:  
- As a tribute to the [Legends of Ultima](https://www.legendsofultima.online/) community server.    

## Is LoUAM compatible with the official Legends of Aria servers?

The community server Legends of Ultima is the reason why this tool exists: we enjoy this community server and have decided to write this tool and name LoUAM after it.  
As of today, LoUAM has only been tested with the Legends of Ultima Community Server.  
LoUAM may or may not work with official Legends of Aria servers run by Citadel Studios.  

## Why is LoUAM not working with the new version of Legends of Aria?

Some Legends of Aria client updates may break the compatibility with LoUAM.  
When you download the latest LoUAM release archive, double check the release notes: there is usually an indication of which version of Legends of Aria client the release is compatible with.  
Upon the release of a new Legends of Aria Client, it may take some time before we update LoUAM to be compatible with the latest Legends of Aria client.  
Volunteers are always welcome :)

## How does it work?

LoUAM.exe injects LOU.dll into the Legends of Aria client process, using a well known technique for injecting assemblies into Mono embedded applications, commonly Unity Engine based games.  
LoUAM.exe acts as the GUI while LOU.dll acts as the commands engine, and they communicate via two shared memory maps:  
- A ClientCommand memory map, where LoUAM.exe queues all the commands that have to be processed and executed by the LoA Client
- A ClientStatus memory map, where LOU.dll updates the status of the LoA Client and the answers to various commands by populating a bunch of variables  
Credits for various components and implementations can be found at the bottom of this page.  

## Can I multibox?

Yes.  
You need to open one instance of LoUAM, lunch a client, and connect LoUAM to it.  
Then you can open another instance of LoUAM, lunch another client, and connect LoUAM to it.  
Rinse, and repeat.  

## How can I build LoUAM?

LoUAM has been compiled for x64 architevture with Visual Studio Community 2017 and .NETFramework Version v4.7.2.  
In order to build it, you need to own a copy of the Legends of Aria client, and you need to copy into the LOU\libs\ folder the following libraries which you can take from the C:\Program Files\Legends of Aria Launcher\Legends of Aria\Legends of Aria_Data\Managed folder (or whatever path you have installed your client into):

Assembly-CSharp-firstpass.dll  
Assembly-CSharp.dll  
CoreUtil.dll  
MessageCore.dll  
protobuf-net.dll  
UnityEngine.CoreModule.dll  
UnityEngine.InputLegacyModule.dll  
UnityEngine.InputModule.dll  
UnityEngine.PhysicsModule.dll  
UnityEngine.UI.dll  

## How can I contribute?

Please Star this repository, Fork it, and engage.  
If you are a developer, GitHub Issues and GitHub PRs are always welcome.  
If you have scripts you want to share with the community, please feel free to reach out, or just create a GitHub Issue and attach the script to it.  

We are hoping to create a new community around this tool, so any form of contribution is more than welcome!

# IMPORTANT DISCLAIMER

By using LoUAM you may be breaching the Terms and Conditions of Citadel Studios, Legends of Aria, Legends of Ultima, or whatever community server you are playing on or service you are using.

***USE AT YOUR OWN RISK***

# IMPORTANT RECOMMENDATIONS

ONLY download LoUAM from its official repository: NEVER accept a copy of LoUAM from other people.  

Keep in mind, there is always a possibility that a malicious version of LoUAM will steal your LOU/LOA or Steam credentials or cause other damage. You assume the risk and full responsibility for all of your actions.  

Also: don't be evil.

***USE AT YOUR OWN RISK***

# CREDITS

Ultima Online is copyright of [Electronic Arts Inc](https://uo.com/).  
Legends of Aria is copyright of [Citadel Studios](https://citadelstudios.net/).  
Legends of Ultima is copyright of [Legends of Ultima](https://www.legendsofultima.online/).  
Lady Binary is a tribute to Lord Binary, who was very active in the UO hacking scene (see for example [UO_RICE](https://github.com/necr0potenc3/UO_RICE)).  
LoUAM is of course inspired by the great [UO Auto-Map](http://www.easyuo.com/).  
The LOU part of LoUAM is a tribute to [Legends Of Ultima](https://www.legendsofultima.online/), whose passionate staff have dedicated so much effort in putting together a wonderful product based off of [Legends of Aria](https://www.legendsofaria.com/).  
The Mono Injection code is based on [SharpMonoInjector](https://github.com/warbler/SharpMonoInjector), commit 73566c1.  

# CONTACTS

You can contact me at ladybinary@protonmail.com, or you can also find me on the LoUAM discord server https://discord.gg/RWAqYcV

License
-------

This project is licensed under a 3-clause BSD license, which can be found in the [LICENSE](LICENSE) file.  
