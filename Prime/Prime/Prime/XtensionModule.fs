﻿[<AutoOpen>]
module XtensionModule
open System.Reflection
open Microsoft.FSharp.Reflection

type XFieldDescriptor =
    { FieldName : Lun
      TypeName : Lun } // the .NET type name

type XType =
    { XTypeName : Lun
      XSubtypeNames : Lun list // the child XType names
      XFieldDescriptors : XFieldDescriptor list }

type XTypes =
    Map<Lun, XType>

type XFields =
    Map<Lun, obj>

/// Xtensions (and their supporting types) are a dynamic, functional, and semi-convenient way to
/// solve the 'expression problem' in F#.
type [<StructuralEqualityAttribute; NoComparison>] Xtension =
    { OptXTypeName : Lun option
      XFields : XFields }

    // NOTE: this could instead be a special class with a MethodMissing method
    static member private EmptyDispatcher =
        new obj ()

    static member (?) (this : Xtension, memberName) : 'a -> 'r =

        // NOTE: I think the explicit args abstraction is required here to satisfy the signature
        // for op_Dynamic... maybe.
        fun args ->

            // check if dynamic member is a method or a field
            match Map.tryFind (Lun.makeFast memberName) this.XFields with
            | None ->
                
                // try to convert method args to an array
                let optArgArray =
                    if box args = null then null // not sure what this line is meant to do
                    elif FSharpType.IsTuple (args.GetType ()) then FSharpValue.GetTupleFields args
                    else [|args|]
                
                // ensure arg array exists
                match optArgArray with
                | null -> failwith "Xtension method call must have at least 1 argument."
                | argArray ->

                    // ensure last arg is a dispatcher container
                    match Array.last argArray with
                    | :? IXDispatcherContainer as context ->

                        // find dispatcher, or use the empty dispatcher
                        let dispatcher = 
                            match this.OptXTypeName with
                            | None -> Xtension.EmptyDispatcher
                            | Some xTypeName ->
                                let dispatchers = context.GetDispatchers ()
                                match Map.tryFind xTypeName dispatchers with
                                | None -> note <| "Invalid dispatcher '" + xTypeName.LunStr + "'."; Xtension.EmptyDispatcher
                                | Some dispatcher -> dispatcher

                        // dispatch method call
                        match (dispatcher.GetType ()).GetMethod memberName with
                        | null -> failwith <| "Could not find method '" + memberName + "'."
                        | aMethod -> aMethod.Invoke (dispatcher, argArray) :?> 'r
                    | _ -> failwith "Last argument of Xtension method call must be an IXDispatcherContainer."

            // just return field
            | Some field -> field :?> 'r

    static member (?<-) (this : Xtension, fieldName, value) =
        let xFields = Map.add (Lun.makeFast fieldName) (value :> obj) this.XFields
        { this with XFields = xFields }

and IXDispatchers =
    Map<Lun, obj>

and IXDispatcherContainer =
    interface
        abstract GetDispatchers : unit -> IXDispatchers
        end

and [<StructuralEquality; NoComparison>] XDispatcherContainer =
    { Dispatchers : IXDispatchers }
    interface IXDispatcherContainer with
        member this.GetDispatchers () = this.Dispatchers
        end