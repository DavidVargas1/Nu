﻿namespace Nu
open System
open Prime
open Nu
open Nu.NuConstants

[<AutoOpen>]
module TransitionModule =

    type Transition with
    
        static member makeDefault transitionType =
            { TransitionLifetime = 0L
              TransitionTicks = 0L
              TransitionType = transitionType
              OptDissolveImage = None }

[<AutoOpen>]
module ScreenModule =

    type Screen with

        static member register (address : Address) (screen : Screen) (world : World) : World = screen?Register (address, world)
        static member unregister (address : Address) (screen : Screen) (world : World) : World = screen?Unregister (address, world)

    type ScreenDispatcher () =

        abstract member Register : Address * World -> World
        default dispatcher.Register (_, world) = world

        abstract member Unregister : Address * World -> World
        default dispatcher.Unregister (_, world) = world

    type Screen with

        static member make dispatcherName =
            { Id = makeId ()
              State = IdlingState
              Incoming = Transition.makeDefault Incoming
              Outgoing = Transition.makeDefault Outgoing
              Xtension = { XFields = Map.empty; OptXDispatcherName = Some dispatcherName; CanDefault = true; Sealed = false }}

        static member makeDissolve dispatcherName incomingTime outgoingTime =
            let optDissolveImage = Some <| { ImageAssetName = "Image8"; PackageName = DefaultPackageName }
            let incomingDissolve = { Transition.makeDefault Incoming with TransitionLifetime = incomingTime; OptDissolveImage = optDissolveImage }
            let outgoingDissolve = { Transition.makeDefault Outgoing with TransitionLifetime = outgoingTime; OptDissolveImage = optDissolveImage }
            let screen = Screen.make dispatcherName
            { screen with Incoming = incomingDissolve; Outgoing = outgoingDissolve }

[<AutoOpen>]
module WorldScreenModule =

    type World with

        static member private optScreenFinder address world =
            match address.AddrList with
            | [screenName] -> Map.tryFind screenName world.Screens
            | _ -> failwith <| "Invalid screen address '" + string address + "'."

        static member private screenAdder address world child =
            match address.AddrList with
            | [screenName] -> { world with Screens = Map.add screenName child world.Screens }
            | _ -> failwith <| "Invalid screen address '" + string address + "'."

        static member private screenRemover address world =
            match address.AddrList with
            | [screenName] -> { world with Screens = Map.remove screenName world.Screens }
            | _ -> failwith <| "Invalid screen address '" + string address + "'."

        static member getScreen address world = Option.get <| World.optScreenFinder address world
        static member setScreen address screen world = World.screenAdder address world screen
        static member getOptScreen address world = World.optScreenFinder address world
        static member containsScreen address world = Option.isSome <| World.getOptScreen address world
        static member private setOptScreen address optScreen world =
            match optScreen with
            | Some screen -> World.setScreen address screen world
            | None -> World.screenRemover address world
            
        static member withScreen fn address world = Sim.withSimulant World.getScreen World.setScreen fn address world
        static member withScreenAndWorld fn address world = Sim.withSimulantAndWorld World.getScreen World.setScreen fn address world
        static member tryWithScreen fn address world = Sim.tryWithSimulant World.getOptScreen World.setScreen fn address world
        static member tryWithScreenAndWorld fn address world = Sim.tryWithSimulantAndWorld World.getOptScreen World.setScreen fn address world

        static member getScreens address world =
            match address.AddrList with
            | [] -> world.Screens
            | _ -> failwith <| "Invalid screen address '" + string address + "'."

        static member registerScreen address (screen : Screen) world =
            Screen.register address screen world

        static member unregisterScreen address world =
            let screen = World.getScreen address world
            Screen.unregister address screen world

        static member removeScreenImmediate address world =
            let world = World.publish4 (RemovingEventName + address) address NoData world
            let world = World.clearGroupsImmediate address world
            let world = World.unregisterScreen address world
            World.setOptScreen address None world

        static member removeScreen address world =
            let task =
                { ScheduledTime = world.TickTime
                  Operation = fun world -> if World.containsScreen address world then World.removeScreenImmediate address world else world }
            { world with Tasks = task :: world.Tasks }

        static member addScreen address screen groupDescriptors world =
            let world =
                match World.getOptScreen address world with
                | Some _ -> World.removeScreenImmediate address world
                | None -> world
            let world = World.setScreen address screen world
            let world = World.addGroups address groupDescriptors world
            let world = World.registerScreen address screen world
            World.publish4 (AddEventName + address) address NoData world