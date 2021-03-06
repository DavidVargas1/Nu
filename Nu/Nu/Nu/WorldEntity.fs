﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2012-2016.

namespace Nu
open System
open System.ComponentModel
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open OpenTK
open Prime
open Nu

[<AutoOpen>]
module WorldEntityModule =

    type Entity with

        member this.GetId world = (World.getEntityState this world).Id
        member this.GetName world = (World.getEntityState this world).Name
        member this.GetXtension world = (World.getEntityState this world).Xtension
        member this.GetDispatcherNp world = (World.getEntityState this world).DispatcherNp
        member this.GetCreationTimeStampNp world = (World.getEntityState this world).CreationTimeStampNp
        member this.GetOptSpecialization world = (World.getEntityState this world).OptSpecialization
        member this.GetOptOverlayName world = (World.getEntityState this world).OptOverlayName
        member this.SetOptOverlayName value world = World.updateEntityState (fun entityState -> { entityState with OptOverlayName = value }) this world
        member this.GetPosition world = (World.getEntityState this world).Position
        member this.SetPosition value world = World.updateEntityStatePlus (fun entityState -> { entityState with Position = value }) this world
        member this.GetSize world = (World.getEntityState this world).Size
        member this.SetSize value world = World.updateEntityStatePlus (fun entityState -> { entityState with Size = value }) this world
        member this.GetRotation world = (World.getEntityState this world).Rotation
        member this.SetRotation value world = World.updateEntityStatePlus (fun entityState -> { entityState with Rotation = value }) this world
        member this.GetDepth world = (World.getEntityState this world).Depth
        member this.SetDepth value world = World.updateEntityState (fun entityState -> { entityState with Depth = value }) this world
        member this.GetOverflow world = (World.getEntityState this world).Overflow
        member this.SetOverflow value world = World.updateEntityStatePlus (fun entityState -> { entityState with Overflow = value }) this world
        member this.GetViewType world = (World.getEntityState this world).ViewType
        member this.SetViewType value world = World.updateEntityStatePlus (fun entityState -> { entityState with ViewType = value }) this world
        member this.GetVisible world = (World.getEntityState this world).Visible
        member this.SetVisible value world = World.updateEntityState (fun entityState -> { entityState with Visible = value }) this world
        member this.GetOmnipresent world = (World.getEntityState this world).Omnipresent
        member this.SetOmnipresent value world = World.updateEntityStatePlus (fun entityState -> { entityState with Omnipresent = value }) this world
        member this.GetPublishUpdatesNp world = (World.getEntityState this world).PublishUpdatesNp
        member this.SetPublishUpdatesNp value world = World.updateEntityState (fun entityState -> { entityState with PublishUpdatesNp = value }) this world
        member this.GetPublishChangesNp world = (World.getEntityState this world).PublishChangesNp
        member this.SetPublishChangesNp value world = World.updateEntityState (fun entityState -> { entityState with PublishChangesNp = value }) this world
        member this.GetPersistent world = (World.getEntityState this world).Persistent
        member this.SetPersistent value world = World.updateEntityState (fun entityState -> { entityState with Persistent = value }) this world
        member this.GetFacetNames world = (World.getEntityState this world).FacetNames
        member this.GetFacetsNp world = (World.getEntityState this world).FacetsNp
        
        /// Get the transform.
        member this.GetTransform world =
            EntityState.getTransform (World.getEntityState this world)

        /// Set the transform.
        member this.SetTransform value world =
            World.updateEntityStatePlus (EntityState.setTransform value) this world

        /// Get a dynamic property and its type information.
        member this.GetProperty propertyName world =
            EntityState.getProperty (World.getEntityState this world) propertyName

        /// Get a dynamic property.
        member this.Get propertyName world : 'a =
            EntityState.get (World.getEntityState this world) propertyName

        /// Set a dynamic property.
        member this.Set propertyName (value : 'a) world = 
            World.setEntityState (EntityState.set (World.getEntityState this world) propertyName value) this world

        /// Attach a dynamic property.
        member this.AttachProperty name value world =
            World.setEntityState (EntityState.attachProperty name value ^ World.getEntityState this world) this world

        /// Detach a dynamic property.
        member this.DetachProperty name world =
            World.setEntityState (EntityState.detachProperty name ^ World.getEntityState this world) this world

        /// Get an entity's bounds, not taking into account its overflow.
        member this.GetBounds world =
            Math.makeBounds
                (this.GetPosition world)
                (this.GetSize world)

        /// Get an entity's bounds, taking into account its overflow.
        member this.GetBoundsOverflow world =
            Math.makeBoundsOverflow
                (this.GetPosition world)
                (this.GetSize world)
                (this.GetOverflow world)

        /// Query than an entity is in the camera's view.
        member this.InView world =
            if not ^ this.GetOmnipresent world then
                let camera = World.getCamera world
                Camera.inView
                    (this.GetViewType world)
                    (this.GetBoundsOverflow world)
                    camera
             else true

        /// Get the center position of an entity.
        member this.GetCenter world =
            let transform = this.GetTransform world
            transform.Position + transform.Size * 0.5f

        /// Set the center position of an entity.
        member this.SetCenter center world =
            let size = this.GetSize world
            this.SetPosition (center - size * 0.5f) world

        /// TODO: document!
        member this.SetPositionSnapped snap position world =
            let snapped = Math.snap2F snap position
            this.SetPosition snapped world

        /// TODO: document!
        member this.SetTransformSnapped positionSnap rotationSnap transform world =
            let transform = Math.snapTransform positionSnap rotationSnap transform
            this.SetTransform transform world

        /// Query that an entity dispatches in the same manner as the dispatcher with the target type.
        member this.DispatchesAs (dispatcherTargetType : Type) world =
            Reflection.dispatchesAs dispatcherTargetType (this.GetDispatcherNp world)

    type World with

        static member private updateEntityPublishChanges entity world =
            let entityChangeEventAddress = entity.ChangeAddress |> atooa
            let publishChanges =
                let subscriptions = Vmap.tryFind entityChangeEventAddress (World.getSubscriptions world)
                match subscriptions with
                | Some [] -> failwithumf () // NOTE: implementation of event system should clean up all empty subscription entries, AFAIK
                | Some (_ :: _) -> true
                | None -> false
            if World.containsEntity entity world
            then entity.SetPublishChangesNp publishChanges world
            else world

        static member private updateEntityPublishUpdates entity world =
            let entityUpdateEventAddress = entity.UpdateAddress |> atooa
            let publishUpdates =
                let subscriptions = Vmap.tryFind entityUpdateEventAddress (World.getSubscriptions world)
                match subscriptions with
                | Some [] -> failwithumf () // NOTE: implementation of event system should clean up all empty subscription entries, AFAIK
                | Some (_ :: _) -> true
                | None -> false
            if World.containsEntity entity world
            then entity.SetPublishUpdatesNp publishUpdates world
            else world

        static member private registerEntity (entity : Entity) world =
            let dispatcher = entity.GetDispatcherNp world : EntityDispatcher
            let facets = entity.GetFacetsNp world
            let world = dispatcher.Register (entity, world)
            let world = List.fold (fun world (facet : Facet) -> facet.Register (entity, world)) world facets
            let world = World.updateEntityPublishChanges entity world
            World.updateEntityPublishUpdates entity world

        static member private unregisterEntity (entity : Entity) world =
            let dispatcher = entity.GetDispatcherNp world : EntityDispatcher
            let facets = entity.GetFacetsNp world
            let world = dispatcher.Unregister (entity, world)
            List.fold (fun world (facet : Facet) -> facet.Unregister (entity, world)) world facets

        static member internal addEntity mayReplace entityState entity world =

            // add entity only if it is new or is explicitly able to be replaced
            let isNew = not ^ World.containsEntity entity world
            if isNew || mayReplace then

                // get old world for entity tree rebuild
                let oldWorld = world
                
                // adding entity to world
                let world = World.addEntityState entityState entity world
                
                // pulling out screen state
                let screen = entity |> etog |> gtos
                let screenState = World.getScreenState screen world

                // mutate entity tree
                let entityTree =
                    MutantCache.mutateMutant
                        (fun () -> World.rebuildEntityTree screen oldWorld)
                        (fun entityTree ->
                            let entityState = World.getEntityState entity world
                            let entityMaxBounds = World.getEntityStateBoundsMax entityState
                            QuadTree.addElement (entityState.Omnipresent || entityState.ViewType = Absolute) entityMaxBounds entity entityTree
                            entityTree)
                        screenState.EntityTreeNp
                let screenState = { screenState with EntityTreeNp = entityTree }
                let world = World.setScreenState screenState screen world

                // register entity if needed
                if isNew then
                    let world = World.registerEntity entity world
                    let eventTrace = EventTrace.record "World" "addEntity" EventTrace.empty
                    World.publish () (Events.EntityAdd ->- entity) eventTrace entity world
                else world

            // handle failure
            else failwith ^ "Adding an entity that the world already contains at address '" + scstring entity.EntityAddress + "'."

        /// Remove an entity in the world. Can be dangerous if existing in-flight publishing depends on the entity's
        /// existence. Consider using World.destroyEntity instead.
        static member internal removeEntity entity world =
            
            // ensure entity exists in the world
            if World.containsEntity entity world then
                
                // publish event and unregister entity
                let eventTrace = EventTrace.record "World" "removeEntity" EventTrace.empty
                let world = World.publish () (Events.EntityRemoving ->- entity) eventTrace entity world
                let world = World.unregisterEntity entity world

                // get old world for entity tree rebuild
                let oldWorld = world
                
                // pulling out screen state
                let screen = entity |> etog |> gtos
                let screenState = World.getScreenState screen world

                // mutate entity tree
                let entityTree =
                    MutantCache.mutateMutant
                        (fun () -> World.rebuildEntityTree screen oldWorld)
                        (fun entityTree ->
                            let entityState = World.getEntityState entity oldWorld
                            let entityMaxBounds = World.getEntityStateBoundsMax entityState
                            QuadTree.removeElement (entityState.Omnipresent || entityState.ViewType = Absolute) entityMaxBounds entity entityTree
                            entityTree)
                        screenState.EntityTreeNp
                let screenState = { screenState with EntityTreeNp = entityTree }
                let world = World.setScreenState screenState screen world

                // remove the entity from the world
                World.removeEntityState entity world

            // pass
            else world

        static member internal updateEntityPublishingFlags eventAddress world =
            let eventNames = Address.getNames eventAddress
            match eventNames with
            | head :: neck :: tail when Name.getNameStr head = "Entity" && Name.getNameStr neck = "Change" ->
                let publishChanges =
                    match Vmap.tryFind eventAddress (EventWorld.getSubscriptions world) with
                    | Some [] -> failwithumf () // NOTE: implementation of event system should clean up all empty subscription entries, AFAIK
                    | Some (_ :: _) -> true
                    | None -> false
                let entity = Entity.proxy ^ ltoa<Entity> tail
                let world = if World.containsEntity entity world then entity.SetPublishChangesNp publishChanges world else world
                world
            | head :: tail when Name.getNameStr head = "Update" ->
                let publishUpdates =
                    match Vmap.tryFind eventAddress (EventWorld.getSubscriptions world) with
                    | Some [] -> failwithumf () // NOTE: implementation of event system should clean up all empty subscription entries, AFAIK
                    | Some (_ :: _) -> true
                    | None -> false
                let entity = Entity.proxy ^ ltoa<Entity> tail
                let world = if World.containsEntity entity world then entity.SetPublishUpdatesNp publishUpdates world else world
                world
            | _ -> world

        static member internal getEntityFacetNamesReflectively entityState =
            List.map getTypeName entityState.FacetsNp

        static member internal updateEntity (entity : Entity) world =
            let dispatcher = entity.GetDispatcherNp world : EntityDispatcher
            let facets = entity.GetFacetsNp world
            let world = dispatcher.Update (entity, world)
            let world = List.foldBack (fun (facet : Facet) world -> facet.Update (entity, world)) facets world
            if entity.GetPublishUpdatesNp world then
                let eventTrace = EventTrace.record "World" "updateEntity" EventTrace.empty
                World.publish7 World.getSubscriptionsSorted World.sortSubscriptionsByHierarchy () entity.UpdateAddress eventTrace Simulants.Game world
            else world

        static member internal actualizeEntity (entity : Entity) world =
            let dispatcher = entity.GetDispatcherNp world : EntityDispatcher
            let facets = entity.GetFacetsNp world
            let world = dispatcher.Actualize (entity, world)
            List.foldBack (fun (facet : Facet) world -> facet.Actualize (entity, world)) facets world

        /// Query that the world contains an entity.
        static member containsEntity entity world =
            Option.isSome ^ World.getOptEntityState entity world

        /// Proxy all the entities contained by a group.
        static member proxyEntities group world =
            match Address.getNames group.GroupAddress with
            | [screenName; groupName] ->
                match Vmap.tryFind screenName ^ World.getScreenDirectory world with
                | Some (_, groupDirectory) ->
                    match Vmap.tryFind groupName groupDirectory with
                    | Some (_, entityDirectory) ->
                        Vmap.fold (fun state _ entityAddress -> Entity.proxy entityAddress :: state) [] entityDirectory :> _ seq
                    | None -> failwith ^ "Invalid group address '" + scstring group.GroupAddress + "'."
                | None -> failwith ^ "Invalid group address '" + scstring group.GroupAddress + "'."
            | _ -> failwith ^ "Invalid group address '" + scstring group.GroupAddress + "'."

        /// Destroy an entity in the world immediately. Can be dangerous if existing in-flight publishing depends on
        /// the entity's existence. Consider using World.destroyEntity instead.
        static member destroyEntityImmediate entity world =
            World.removeEntity entity world

        /// Destroy an entity in the world on the next tick. Use this rather than destroyEntityImmediate unless you
        /// need the latter's specific behavior.
        static member destroyEntity entity world =
            let tasklet =
                { ScheduledTime = World.getTickTime world
                  Command = { Execute = fun world -> World.destroyEntityImmediate entity world }}
            World.addTasklet tasklet world

        /// Destroy multiple entities in the world immediately. Can be dangerous if existing in-flight publishing
        /// depends on any of the entities' existences. Consider using World.destroyEntities instead.
        static member destroyEntitiesImmediate entities world =
            List.foldBack
                (fun entity world -> World.destroyEntityImmediate entity world)
                (List.ofSeq entities)
                world

        /// Destroy multiple entities in the world. Use this rather than destroyEntitiesImmediate unless you need the
        /// latter's specific behavior.
        static member destroyEntities entities world =
            let tasklet =
                { ScheduledTime = World.getTickTime world
                  Command = { Execute = fun world -> World.destroyEntitiesImmediate entities world }}
            World.addTasklet tasklet world

        /// Reassign an entity's identity and / or group. Note that since this destroys the reassigned entity
        /// immediately, you should not call this inside an event handler that involves the reassigned entity itself.
        static member reassignEntity entity optName group world =
            let entityState = World.getEntityState entity world
            let world = World.removeEntity entity world
            let id = makeGuid ()
            let name = match optName with Some name -> name | None -> Name.make ^ scstring id
            let entityState = { entityState with Id = id; Name = name }
            let transmutedEntity = gtoe group name
            let world = World.addEntity false entityState transmutedEntity world
            (transmutedEntity, world)

        /// Create an entity and add it to the world.
        static member createEntity dispatcherName optSpecialization optName group world =

            // grab overlay dependencies
            let overlayer = World.getOverlayer world
            let overlayRouter = World.getOverlayRouter world

            // find the entity's dispatcher
            let dispatchers = World.getEntityDispatchers world
            let dispatcher = Map.find dispatcherName dispatchers
            
            // compute the default opt overlay name
            let intrinsicOverlayName = dispatcherName
            let defaultOptOverlayName = OverlayRouter.findOptOverlayName intrinsicOverlayName overlayRouter

            // make the bare entity state (with name as id if none is provided)
            let entityState = EntityState.make optSpecialization optName defaultOptOverlayName dispatcher

            // attach the entity state's intrinsic facets and their properties
            let entityState = World.attachIntrinsicFacetsViaNames entityState world

            // apply the entity state's overlay to its facet names
            let entityState =
                match defaultOptOverlayName with
                | Some defaultOverlayName ->

                    // apply overlay to facets
                    let entityState = Overlayer.applyOverlayToFacetNames EntityState.copy intrinsicOverlayName defaultOverlayName entityState overlayer overlayer

                    // synchronize the entity's facets (and attach their properties)
                    match World.trySynchronizeFacetsToNames Set.empty entityState None world with
                    | Right (entityState, _) -> entityState
                    | Left error -> Log.debug error; entityState
                | None -> entityState

            // attach the entity state's dispatcher properties
            let entityState = Reflection.attachProperties EntityState.copy dispatcher entityState

            // apply the entity state's overlay
            let entityState =
                match entityState.OptOverlayName with
                | Some overlayName ->
                    // OPTIMIZATION: apply overlay only when it will change something (EG - when it's not the intrinsic overlay)
                    if intrinsicOverlayName <> overlayName then
                        let facetNames = World.getEntityFacetNamesReflectively entityState
                        Overlayer.applyOverlay EntityState.copy intrinsicOverlayName overlayName facetNames entityState overlayer
                    else entityState
                | None -> entityState

            // add entity's state to world
            let entity = gtoe group entityState.Name
            let world = World.addEntity false entityState entity world
            (entity, world)

        /// Propagate an entity's physics properties to the physics subsystem.
        static member propagateEntityPhysics (entity : Entity) world =
            let dispatcher = entity.GetDispatcherNp world
            let facets = entity.GetFacetsNp world
            let world = dispatcher.PropagatePhysics (entity, world)
            List.fold (fun world (facet : Facet) -> facet.PropagatePhysics (entity, world)) world facets

        /// Get the quick size of an entity (the appropriate user-defined size for an entity).
        static member getEntityQuickSize (entity : Entity) world =
            let dispatcher = entity.GetDispatcherNp world : EntityDispatcher
            let facets = entity.GetFacetsNp world
            let quickSize = dispatcher.GetQuickSize (entity, world)
            List.fold
                (fun (maxSize : Vector2) (facet : Facet) ->
                    let quickSize = facet.GetQuickSize (entity, world)
                    Vector2
                        (Math.Max (quickSize.X, maxSize.X),
                         Math.Max (quickSize.Y, maxSize.Y)))
                quickSize
                facets

        /// Get an entity's picking priority.
        static member getEntityPickingPriority (participant : Participant) world =
            match participant with
            | :? Entity as entity ->
                let entityState = World.getEntityState entity world
                let dispatcher = entityState.DispatcherNp
                dispatcher.GetPickingPriority (entity, entityState.Depth, world)
            | _ -> failwithumf ()

        /// Sort subscriptions by their editor picking priority.
        static member sortSubscriptionsByPickingPriority subscriptions world =
            World.sortSubscriptionsBy World.getEntityPickingPriority subscriptions world

        /// TODO: document!
        static member pickingSortEntities entities world =
            let entities = List.ofSeq entities
            let prioritiesAndEntities = List.map (fun (entity : Entity) -> (World.getEntityPickingPriority entity world, entity)) entities
            let prioritiesAndEntities = List.sortWith Pair.sortFstDescending prioritiesAndEntities
            List.map snd prioritiesAndEntities

        /// TODO: document!
        static member tryPickEntity position entities world =
            let entitiesSorted = World.pickingSortEntities entities world
            List.tryFind
                (fun (entity : Entity) ->
                    let positionWorld = World.getCameraBy (Camera.mouseToWorld (entity.GetViewType world) position) world
                    let picked = Math.isPointInBounds positionWorld (entity.GetBounds world)
                    picked)
                entitiesSorted

        /// Try to set an entity's optional overlay name.
        static member trySetEntityOptOverlayName optOverlayName entity world =
            let oldEntityState = World.getEntityState entity world
            let oldOptOverlayName = oldEntityState.OptOverlayName
            let entityState = { oldEntityState with OptOverlayName = optOverlayName }
            match (oldOptOverlayName, optOverlayName) with
            | (Some oldOverlayName, Some overlayName) ->
                let overlayer = World.getOverlayer world
                let (entityState, world) =
                    let entityState = Overlayer.applyOverlayToFacetNames EntityState.copy oldOverlayName overlayName entityState overlayer overlayer
                    match World.trySynchronizeFacetsToNames entityState.FacetNames entityState (Some entity) world with
                    | Right (entityState, world) -> (entityState, world)
                    | Left error -> Log.debug error; (entityState, world)
                let facetNames = World.getEntityFacetNamesReflectively entityState
                let entityState = Overlayer.applyOverlay EntityState.copy oldOverlayName overlayName facetNames entityState overlayer
                let oldWorld = world
                let world = World.setEntityStateWithoutEvent entityState entity world
                let world = World.updateEntityInEntityTree entity oldWorld world
                let world = World.publishEntityChange entityState entity oldWorld world
                Right world
            | (_, _) -> let _ = World.choose world in Left "Could not set the entity's overlay name."

        /// Try to set the entity's facet names.
        static member trySetEntityFacetNames facetNames entity world =
            let entityState = World.getEntityState entity world
            match World.trySetFacetNames facetNames entityState (Some entity) world with
            | Right (entityState, world) ->
                let oldWorld = world
                let world = World.setEntityStateWithoutEvent entityState entity world
                let world = World.updateEntityInEntityTree entity oldWorld world
                let world = World.publishEntityChange entityState entity oldWorld world
                Right world
            | Left error -> Left error

        /// Write an entity to an entity descriptor.
        static member writeEntity (entity : Entity) entityDescriptor world =
            let entityState = World.getEntityState entity world
            let entityDispatcherName = getTypeName entityState.DispatcherNp
            let entityDescriptor = { entityDescriptor with EntityDispatcher = entityDispatcherName }
            let shouldWriteProperty = fun propertyName propertyType (propertyValue : obj) ->
                if propertyName = "OptOverlayName" && propertyType = typeof<string option> then
                    let overlayRouter = World.getOverlayRouter world
                    let defaultOptOverlayName = OverlayRouter.findOptOverlayName entityDispatcherName overlayRouter
                    defaultOptOverlayName <> (propertyValue :?> string option)
                else
                    let overlayer = World.getOverlayer world
                    let facetNames = World.getEntityFacetNamesReflectively entityState
                    Overlayer.shouldPropertySerialize5 facetNames propertyName propertyType entityState overlayer
            let getEntityProperties = Reflection.writeMembersFromTarget shouldWriteProperty entityDescriptor.EntityProperties entityState
            { entityDescriptor with EntityProperties = getEntityProperties }

        /// Write multiple entities to a group descriptor.
        static member writeEntities entities groupDescriptor world =
            entities |>
            Seq.sortBy (fun (entity : Entity) -> entity.GetCreationTimeStampNp world) |>
            Seq.filter (fun (entity : Entity) -> entity.GetPersistent world) |>
            Seq.fold (fun entityDescriptors entity -> World.writeEntity entity EntityDescriptor.empty world :: entityDescriptors) groupDescriptor.Entities |>
            fun entityDescriptors -> { groupDescriptor with Entities = entityDescriptors }

        /// Read an entity from an entity descriptor.
        static member readEntity entityDescriptor optName group world =

            // grab overlay dependencies
            let overlayer = World.getOverlayer world
            let overlayRouter = World.getOverlayRouter world

            // create the dispatcher
            let dispatcherName = entityDescriptor.EntityDispatcher
            let dispatchers = World.getEntityDispatchers world
            let (dispatcherName, dispatcher) =
                match Map.tryFind dispatcherName dispatchers with
                | Some dispatcher -> (dispatcherName, dispatcher)
                | None ->
                    Log.info ^ "Could not locate dispatcher '" + dispatcherName + "'."
                    let dispatcherName = typeof<EntityDispatcher>.Name
                    let dispatcher = Map.find dispatcherName dispatchers
                    (dispatcherName, dispatcher)

            // compute the default overlay names
            let intrinsicOverlayName = dispatcherName
            let defaultOptOverlayName = OverlayRouter.findOptOverlayName intrinsicOverlayName overlayRouter

            // make the bare entity state with name as id
            let entityState = EntityState.make None None defaultOptOverlayName dispatcher

            // attach the entity state's intrinsic facets and their properties
            let entityState = World.attachIntrinsicFacetsViaNames entityState world

            // read the entity state's overlay and apply it to its facet names if applicable
            let entityState = Reflection.tryReadOptOverlayNameToTarget EntityState.copy entityDescriptor.EntityProperties entityState
            let entityState =
                match (defaultOptOverlayName, entityState.OptOverlayName) with
                | (Some defaultOverlayName, Some overlayName) -> Overlayer.applyOverlayToFacetNames EntityState.copy defaultOverlayName overlayName entityState overlayer overlayer
                | (_, _) -> entityState

            // read the entity state's facet names
            let entityState = Reflection.readFacetNamesToTarget EntityState.copy entityDescriptor.EntityProperties entityState

            // attach the entity state's dispatcher properties
            let entityState = Reflection.attachProperties EntityState.copy dispatcher entityState
            
            // synchronize the entity state's facets (and attach their properties)
            let entityState =
                match World.trySynchronizeFacetsToNames Set.empty entityState None world with
                | Right (entityState, _) -> entityState
                | Left error -> Log.debug error; entityState

            // attempt to apply the entity state's overlay
            let entityState =
                match entityState.OptOverlayName with
                | Some overlayName ->
                    // OPTIMIZATION: applying overlay only when it will change something (EG - when it's not the default overlay)
                    if intrinsicOverlayName <> overlayName then
                        let facetNames = World.getEntityFacetNamesReflectively entityState
                        Overlayer.applyOverlay EntityState.copy intrinsicOverlayName overlayName facetNames entityState overlayer
                    else entityState
                | None -> entityState

            // read the entity state's values
            let entityState = Reflection.readMembersToTarget EntityState.copy entityDescriptor.EntityProperties entityState

            // apply the name if one is provided
            let entityState =
                match optName with
                | Some name -> { entityState with Name = name }
                | None -> entityState

            // add entity state to the world
            let entity = gtoe group entityState.Name
            let world = World.addEntity true entityState entity world
            (entity, world)

        /// Read multiple entities from a group descriptor.
        static member readEntities groupDescriptor group world =
            List.foldBack
                (fun entityDescriptor (entities, world) ->
                    let (entity, world) = World.readEntity entityDescriptor None group world
                    (entity :: entities, world))
                    groupDescriptor.Entities
                    ([], world)

    /// Represents the member value of an entity as accessible via reflection.
    type [<ReferenceEquality>] EntityMemberValue =
        | EntityXPropertyDescriptor of XPropertyDescriptor
        | EntityPropertyInfo of PropertyInfo

        /// Query that an entity contains the given property.
        static member containsProperty (property : PropertyInfo) =
            let properties = typeof<EntityState>.GetProperties property.Name
            Seq.exists (fun item -> item = property) properties

        /// Get the entity's property value.
        static member getValue property (entity : Entity) world =
            match property with
            | EntityXPropertyDescriptor xfd ->
                let xtension = entity.GetXtension world
                (Xtension.getProperty xfd.PropertyName xtension).PropertyValue
            | EntityPropertyInfo propertyInfo ->
                let entityState = World.getEntityState entity world
                propertyInfo.GetValue entityState

        /// Set the entity's property value.
        static member setValue property value (entity : Entity) world =
            match property with
            | EntityXPropertyDescriptor xfd ->
                let xProperty = { PropertyValue = value; PropertyType = xfd.PropertyType }
                entity.AttachProperty xfd.PropertyName xProperty world
            | EntityPropertyInfo propertyInfo ->
                let entityState = World.getEntityState entity world
                let entityState = EntityState.copy entityState
                propertyInfo.SetValue (entityState, value)
                let oldWorld = world
                let world = World.setEntityStateWithoutEvent entityState entity world
                let world = World.updateEntityInEntityTree entity oldWorld world
                World.publishEntityChange entityState entity oldWorld world

        // TODO: put this in a better place! And of course, document.
        static member getPropertyDescriptors makePropertyDescriptor optXtension =
            // OPTIMIZATION: seqs used for speed.
            let properties = typeof<EntityState>.GetProperties ()
            let typeConverterAttribute = TypeConverterAttribute (typeof<SymbolicConverter>) // TODO: make this static?
            let properties = Seq.filter (fun (property : PropertyInfo) -> property.PropertyType <> typeof<Xtension>) properties
            let properties = Seq.filter (fun (property : PropertyInfo) -> Seq.isEmpty ^ property.GetCustomAttributes<ExtensionAttribute> ()) properties
            let properties = Seq.filter (fun (property : PropertyInfo) -> Reflection.isPropertyPersistentByName property.Name) properties
            let propertyDescriptors = Seq.map (fun property -> makePropertyDescriptor (EntityPropertyInfo property, [|typeConverterAttribute|])) properties
            let propertyDescriptors =
                match optXtension with
                | Some xtension ->
                    let xPropertyDescriptors =
                        Seq.fold
                            (fun xPropertyDescriptors (xPropertyName, xProperty : XProperty) ->
                                let xPropertyType = xProperty.PropertyType
                                if Reflection.isPropertyPersistentByName xPropertyName then
                                    let xPropertyDescriptor = EntityXPropertyDescriptor { PropertyName = xPropertyName; PropertyType = xPropertyType }
                                    let xPropertyDescriptor : System.ComponentModel.PropertyDescriptor = makePropertyDescriptor (xPropertyDescriptor, [|typeConverterAttribute|])
                                    xPropertyDescriptor :: xPropertyDescriptors
                                else xPropertyDescriptors)
                            []
                            (Xtension.toSeq xtension)
                    Seq.append xPropertyDescriptors propertyDescriptors
                | None -> propertyDescriptors
            List.ofSeq propertyDescriptors

namespace Debug
open Prime
open Nu
open System.Reflection
open System.Collections.Generic
type Entity =

    /// Provides a view of all the built-in properties of an entity. Useful for debugging such as with
    /// the Watch feature in Visual Studio.
    static member viewProperties entity world =
        let state = World.getEntityState entity world
        state |>
        getType |>
        getProperties |>
        Array.map (fun (property : PropertyInfo) -> (property.Name, property.GetValue state))
        
    /// Provides a view of all the xtension properties of an entity. Useful for debugging such as
    /// with the Watch feature in Visual Studio.
    static member viewXProperties entity world =
        let state = World.getEntityState entity world
        Xtension.toSeq state.Xtension |>
        Array.ofSeq |>
        Array.sortBy fst |>
        Array.map (fun (name, property) -> (name, property.PropertyValue))

    /// Provides a full view of all the member values of an entity. Useful for debugging such
    /// as with the Watch feature in Visual Studio.
    static member view entity world =
        Array.append (Entity.viewProperties entity world) (Entity.viewXProperties entity world)