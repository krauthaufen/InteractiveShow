namespace Aardvark.DotNet.Interactive

open Aardvark.Base
open FSharp.Data.Adaptive
open Adaptify
open Aardvark.SceneGraph
open Aardvark.UI
open Aardvark.UI.Primitives

[<ModelType>]
type SceneModel =
    {
        scene           : ISg
        bounds          : Box3d
        fieldOfView     : float
        camera          : CameraControllerState
        fullscreen      : bool
    }

