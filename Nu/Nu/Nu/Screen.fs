﻿namespace Nu
open System
open System.IO
open System.Xml
open Prime
open Nu
open Nu.Constants
open Nu.WorldConstants

[<AutoOpen>]
module ScreenModule =

    type [<StructuralEquality; NoComparison>] DissolveData =
        { IncomingTime : int64
          OutgoingTime : int64
          DissolveImage : AssetTag }

    type [<StructuralEquality; NoComparison>] SplashData =
        { DissolveData : DissolveData
          IdlingTime : int64
          SplashImage : AssetTag }

    type Screen with

        static member getScreenStateNp (screen : Screen) = screen.ScreenStateNp
        static member setScreenStateNp value screen = { screen with ScreenStateNp = value }
        static member getTransitionTicksNp (screen : Screen) = screen.TransitionTicksNp
        static member setTransitionTicksNp value screen = { screen with TransitionTicksNp = value }
        static member getIncoming (screen : Screen) = screen.Incoming
        static member setIncoming value screen = { screen with Incoming = value }
        static member getOutgoing (screen : Screen) = screen.Outgoing
        static member setOutgoing value screen = { screen with Outgoing = value }
        static member getPersistent (screen : Screen) = screen.Persistent
        static member setPersistent value (screen : Screen) = { screen with Persistent = value }

        static member register (screen : Screen) address world =
            screen.DispatcherNp.Register (screen, address, world)

        static member unregister (screen : Screen) address world =
            screen.DispatcherNp.Unregister (screen, address, world)

        static member isIdling screen =
            screen.ScreenStateNp = IdlingState

        static member dispatchesAs (dispatcherTargetType : Type) (screen : Screen) =
            Reflection.dispatchesAs dispatcherTargetType screen.DispatcherNp

        static member make dispatcher optName =
            let id = Core.makeId ()
            { Id = id
              Name = match optName with None -> acstring id | Some name -> name
              ScreenStateNp = IdlingState
              TransitionTicksNp = 0L // TODO: roll this field into Incoming/OutcomingState values
              Incoming = Transition.make Incoming
              Outgoing = Transition.make Outgoing
              Persistent = true
              CreationTimeNp = DateTime.UtcNow
              DispatcherNp = dispatcher
              Xtension = { XFields = Map.empty; CanDefault = false; Sealed = true } }

[<AutoOpen>]
module WorldScreenModule =

    type World with

        static member private optScreenFinder (address : Screen Address) world =
            match address.Names with
            | [screenName] ->
                match Map.tryFind screenName world.Simulants with
                | Some (screen, _) -> Some screen
                | None -> None
            | _ -> failwith <| "Invalid screen address '" + acstring address + "'."

        static member private screenAdder (screen : Screen) (address : Screen Address) world =
            match address.Names with
            | [screenName] ->
                match Map.tryFind screenName world.Simulants with
                | Some (_, groupMap) -> { world with Simulants = Map.add screenName (screen, groupMap) world.Simulants }
                | None -> { world with Simulants = Map.add screenName (screen, Map.empty) world.Simulants }
            | _ -> failwith <| "Invalid screen address '" + acstring address + "'."

        static member private screenRemover (address : Screen Address) world =
            match address.Names with
            | [screenName] ->
                match Map.tryFind screenName world.Simulants with
                | Some (_, groupMap) ->
                    if Map.isEmpty groupMap
                    then { world with Simulants = Map.remove screenName world.Simulants }
                    else failwith <| "Cannot remove screen " + acstring address + ", which still contains groups."
                | None -> world
            | _ -> failwith <| "Invalid screen address '" + acstring address + "'."

        static member containsScreen address world =
            Option.isSome <| World.optScreenFinder address world

        static member getOptScreen address world =
            World.optScreenFinder address world

        static member getScreenBy by address world =
            by ^^ Option.get ^^ World.getOptScreen address world
        
        static member getScreen address world =
            World.getScreenBy id address world

        static member getOptScreenHierarchy address world =
            match World.getOptScreen address world with
            | Some screen ->
                let groupMap = World.getGroupMapInScreen address world
                Some (screen, groupMap)
            | None -> None
        
        static member getScreenHierarchy address world =
            Option.get <| World.getOptScreenHierarchy address world

        static member getScreenAddress screenName world =
            let address = ntoa<Screen> screenName
            ignore <| World.getScreen address world // ensures address is valid
            address

        static member setScreen screen address world =
            World.screenAdder screen address world

        static member private setOptScreen optScreen address world =
            match optScreen with
            | Some screen -> World.setScreen screen address world
            | None -> World.screenRemover address world

        static member updateOptScreenW updater address world =
            match World.getOptScreen address world with
            | Some screen ->
                let screen = updater screen world
                World.setScreen screen address world
            | None -> world

        static member updateOptScreen updater address world =
            World.updateOptScreenW (fun screen _ -> updater screen) address world

        static member updateByOptScreen updater address world : World =
            match World.getOptScreen address world with
            | Some screen -> updater screen world
            | None -> world

        static member updateScreenW updater address world =
            let screen = World.getScreen address world
            let screen = updater screen world
            World.setScreen screen address world

        static member updateScreen updater address world =
            World.updateScreenW (fun screen _ -> updater screen) address world

        static member updateByScreen updater address world : World =
            let screen = World.getScreen address world
            updater screen world

        static member getScreenHierarchies addresses world =
            Seq.map (fun address -> World.getScreenHierarchy address world) addresses

        static member getScreenMap world =
            world.Simulants

        static member getScreensBy by addresses world =
            Seq.map (fst >> by) <| World.getScreenHierarchies addresses world

        static member getScreens addresses world =
            World.getScreensBy id addresses world

        static member getScreenAddresses world =
            Map.fold (fun addresses screenName _ -> ntoa<Screen> screenName :: addresses) [] world.Simulants

        static member updateScreensW updater addresses world =
            Seq.fold (fun world address -> World.updateScreenW updater address world) world addresses
        
        static member updateScreens updater addresses world =
            World.updateScreenW (fun screen _ -> updater screen) addresses world

        static member filterScreenAddressesW pred addresses world =
            Seq.filter (fun address -> World.getScreenBy (fun screen -> pred screen world) address world) addresses

        static member filterScreenAddresses pred addresses world =
            World.filterScreenAddressesW (fun screen _ -> pred screen) addresses world

        static member private registerScreen screen address world =
            Screen.register screen address world

        static member private unregisterScreen screen address world =
            Screen.unregister screen address world

        static member removeScreenImmediate address world =
            let world = World.publish4 () (RemovingEventAddress ->>- address) address world
            match World.getOptScreen address world with
            | Some screen ->
                let (screen, world) = World.unregisterScreen screen address world
                let groupAddresses = World.getGroupAddressesInScreen address world
                let world = snd <| World.removeGroupsImmediate groupAddresses world
                let world = World.setOptScreen None address world
                (Some screen, world)
            | None -> (None, world)

        static member removeScreen address world =
            let task =
                { ScheduledTime = world.State.TickTime
                  Operation = fun world -> snd <| World.removeScreenImmediate address world }
            World.addTask task world

        static member addScreen screenHierarchy address world =
            let (screen, groupHierarchies) = screenHierarchy
            if not <| World.containsScreen address world then
                let world = World.setScreen screen address world
                let world = snd <| World.addGroups groupHierarchies address world
                let (screen, world) = World.registerScreen screen address world
                let world = World.publish4 () (AddEventAddress ->>- address) address world
                (screen, world)
            else failwith <| "Adding a screen that the world already contains at address '" + acstring address + "'."

        static member makeScreen dispatcherName optName world =
            let dispatcher = Map.find dispatcherName world.Components.ScreenDispatchers
            let screen = Screen.make dispatcher optName
            Reflection.attachFields dispatcher screen
            screen
        
        static member makeDissolveScreen dissolveData dispatcherName optName world =
            let optDissolveImage = Some dissolveData.DissolveImage
            let screen = World.makeScreen dispatcherName optName world
            let incomingDissolve = { Transition.make Incoming with TransitionLifetime = dissolveData.IncomingTime; OptDissolveImage = optDissolveImage }
            let outgoingDissolve = { Transition.make Outgoing with TransitionLifetime = dissolveData.OutgoingTime; OptDissolveImage = optDissolveImage }
            { screen with Incoming = incomingDissolve; Outgoing = outgoingDissolve }

        static member writeScreenHierarchy (writer : XmlWriter) screenHierarchy world =
            let (screen : Screen, groupHierarchies) = screenHierarchy
            writer.WriteAttributeString (DispatcherNameAttributeName, (screen.DispatcherNp.GetType ()).Name)
            Serialization.writePropertiesFromTarget tautology3 writer screen
            writer.WriteStartElement GroupsNodeName
            World.writeGroupHierarchies writer groupHierarchies world
            writer.WriteEndElement ()

        static member writeScreenHierarchies (writer : XmlWriter) screenHierarchies world =
            let screenHierarchies =
                List.sortBy
                    (fun (screen : Screen, _) -> screen.CreationTimeNp)
                    (Map.toValueList screenHierarchies)
            let screenHierarchies = List.filter (fun (screen : Screen, _) -> screen.Persistent) screenHierarchies
            for screenHierarchy in screenHierarchies do
                writer.WriteStartElement ScreenNodeName
                World.writeScreenHierarchy writer screenHierarchy world
                writer.WriteEndElement ()

        static member writeScreenHierarchyToFile (filePath : string) screenHierarchy world =
            let filePathTmp = filePath + ".tmp"
            let writerSettings = XmlWriterSettings ()
            writerSettings.Indent <- true
            use writer = XmlWriter.Create (filePathTmp, writerSettings)
            writer.WriteStartElement RootNodeName
            writer.WriteStartElement ScreenNodeName
            World.writeScreenHierarchy writer screenHierarchy world
            writer.WriteEndElement ()
            writer.WriteEndElement ()
            writer.Dispose ()
            File.Delete filePath
            File.Move (filePathTmp, filePath)

        static member readScreenHierarchy
            (screenNode : XmlNode) defaultDispatcherName defaultGroupDispatcherName defaultEntityDispatcherName world =
            let dispatcherName = Serialization.readDispatcherName defaultDispatcherName screenNode
            let dispatcher =
                match Map.tryFind dispatcherName world.Components.ScreenDispatchers with
                | Some dispatcher -> dispatcher
                | None ->
                    note <| "Could not locate dispatcher '" + dispatcherName + "'."
                    let dispatcherName = typeof<ScreenDispatcher>.Name
                    Map.find dispatcherName world.Components.ScreenDispatchers
            let screen = Screen.make dispatcher None
            Reflection.attachFields screen.DispatcherNp screen
            Serialization.readPropertiesToTarget screenNode screen
            let groupHierarchies = World.readGroupHierarchies (screenNode : XmlNode) defaultGroupDispatcherName defaultEntityDispatcherName world
            (screen, groupHierarchies)

        static member readScreenHierarchies
            (parentNode : XmlNode) defaultDispatcherName defaultGroupDispatcherName defaultEntityDispatcherName world =
            match parentNode.SelectSingleNode ScreensNodeName with
            | null -> Map.empty
            | screensNode ->
                let screenNodes = screensNode.SelectNodes ScreenNodeName
                Seq.fold
                    (fun screenHierarchies screenNode ->
                        let screenHierarchy =
                            World.readScreenHierarchy
                                screenNode
                                defaultDispatcherName
                                defaultGroupDispatcherName
                                defaultEntityDispatcherName
                                world
                        let screenName = (fst screenHierarchy).Name
                        Map.add screenName screenHierarchy screenHierarchies)
                    Map.empty
                    (enumerable screenNodes)

        static member readScreenHierarchyFromFile (filePath : string) world =
            use reader = XmlReader.Create filePath
            let document = let emptyDoc = XmlDocument () in (emptyDoc.Load reader; emptyDoc)
            let rootNode = document.[RootNodeName]
            let screenNode = rootNode.[ScreenNodeName]
            World.readScreenHierarchy
                screenNode
                typeof<ScreenDispatcher>.Name
                typeof<GroupDispatcher>.Name
                typeof<EntityDispatcher>.Name
                world