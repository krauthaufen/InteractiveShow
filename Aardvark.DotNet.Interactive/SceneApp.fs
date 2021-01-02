namespace Aardvark.DotNet.Interactive

open Aardvark.Base
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open Aardvark.SceneGraph.Semantics
open Aardvark.Base.Rendering
open Aardvark.UI
open Aardvark.UI.Primitives

module SceneApp =
    
    type Message =
        | Camera of FreeFlyController.Message
        | ResetCamera
        | EnterFullscreen
        | ExitFullscreen

    let initial (sg : ISg) =
        let bounds = sg.GlobalBoundingBox Ag.Scope.Root |> AVal.force

        let center = bounds.Center
        let size = bounds.Size
        let off = size * 0.9

        let relativeSize = Vec.length size / sqrt 2.0
        {
            scene = sg
            bounds = bounds
            fieldOfView = 60.0
            fullscreen = false
            camera = 
                {
                    FreeFlyController.initial with
                        freeFlyConfig = 
                            { FreeFlyConfig.initial with 
                                moveSensitivity = log relativeSize
                                panMouseSensitivity = 0.25 * relativeSize * FreeFlyConfig.initial.panMouseSensitivity
                                dollyMouseSensitivity = 0.25 * relativeSize * FreeFlyConfig.initial.dollyMouseSensitivity
                                zoomMouseWheelSensitivity = 0.25 * relativeSize * FreeFlyConfig.initial.zoomMouseWheelSensitivity
                            }
                        view = CameraView.lookAt (center + off) center V3d.OOI
                        orbitCenter = Some center
                }
        }

    let update (model : SceneModel) (msg : Message) =
        match msg with
        | Camera msg ->
            { model with camera = FreeFlyController.update model.camera msg }
        | ResetCamera ->
            { model with camera = (initial model.scene).camera }
        | EnterFullscreen ->
            Log.warn "fullscreen"
            { model with fullscreen = true }
        | ExitFullscreen ->
            Log.warn "normal"
            { model with fullscreen = false }

    let view (model : AdaptiveSceneModel) =
        let scene = Sg.dynamic (AVal.map Sg.noEvents model.scene)
        let frustum = 
            (model.camera.view, model.bounds, model.fieldOfView) |||> AVal.map3 (fun cam bounds fov ->
                let (vMin, vMax) = bounds.GetMinMaxInDirection(cam.Forward)
                let far = Vec.dot cam.Forward (vMax - cam.Location) |> max 1.0
                let near = Vec.dot cam.Forward (vMin - cam.Location) |> max (0.0001 * far)
                Frustum.perspective fov near far 1.0
            )


        let requestFullScreen =
            AttributeValue.Event {
                clientSide = fun send id ->
                    String.concat "" [
                        "let ctrl = this;"
                        "while(ctrl && !ctrl.classList.contains('fullscreenable')) { ctrl = ctrl.parentElement; }"
                        "if(ctrl) {"
                        "  if(document.fullscreenElement) {"
                        "    document.onfullscreenchange = function(e) {"
                        "      " + send id ["document.fullscreenElement == ctrl"]
                        "    };"
                        "    document.exitFullscreen();"
                        "  }"
                        "  else {"
                        "    document.onfullscreenchange = function(e) {"
                        "      " + send id ["document.fullscreenElement == ctrl"]
                        "    };"
                        "    if(ctrl) ctrl.requestFullscreen();"
                        "  }"
                        "}"
                        
                    ]
                serverSide = fun _ _ args -> 
                    match args with
                    | ["true"] -> Seq.singleton EnterFullscreen
                    | ["false"] -> Seq.singleton ExitFullscreen
                    | _ -> Seq.empty
            }

        let atts =
            AttributeMap.ofList [
                style "width: 100%; height: 100%"
                attribute "data-samples" "8"
                "ondblclick", requestFullScreen
            ]

        require Html.semui (
            div [ style "position: relative; width: 720px; height: 405px"; clazz "fullscreenable"] [
                FreeFlyController.controlledControl model.camera Camera frustum atts scene

                div [ style "position: absolute; bottom: 0px; right: 0px"] [
                    div [ clazz "ui icon buttons" ] [
                        button [ clazz "ui basic grey icon button"; "onclick", requestFullScreen ] [
                            Incremental.i (
                                AttributeMap.ofListCond [
                                    onlyWhen model.fullscreen (clazz "compress icon")
                                    onlyWhen (AVal.map not model.fullscreen) (clazz "expand icon")
                                ]
                            ) AList.empty
                        ]
                        button [ clazz "ui basic grey icon button" ] [
                            i [ clazz "undo icon"; onClick (fun () -> ResetCamera) ] []
                        ]
                    ]
                ]

            ]
        )


    let app (sg : ISg) =
        {
            initial = initial sg
            update = update
            view = view
            threads = fun m -> FreeFlyController.threads m.camera
            unpersist = Unpersist.instance
        }
