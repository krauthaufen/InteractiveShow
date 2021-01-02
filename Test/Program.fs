open System

open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.UI
open Aardvark.SceneGraph

open Suave
open Suave.WebPart
open Aardium
open Aardvark.DotNet.Interactive
open Aardvark.Base.Rendering



[<EntryPoint; STAThread>]
let main argv = 
    Aardvark.Init()
    Aardium.init()


    use app = new OpenGlApplication()
    let runtime = app.Runtime :> IRuntime
    
    let app = 
        SceneApp.app (
            Sg.box' C4b.Red (Box3d.FromCenterAndSize(V3d.Zero, 1000.0 * V3d.III))
            |> Sg.shader {
                do! DefaultSurfaces.trafo
                do! DefaultSurfaces.simpleLighting
            }
        )

    let instance = 
        app |> App.start

    WebPart.startServer 4321 [ 
        MutableApp.toWebPart' runtime false instance
        Suave.Files.browseHome
    ] |> ignore
    

    Aardium.run {
        url "http://localhost:4321/"
        width 1024
        height 768
        debug true
    }
    0 
