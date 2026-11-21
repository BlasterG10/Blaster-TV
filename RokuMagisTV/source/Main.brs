' Punto de entrada del canal. Crea la pantalla raíz de SceneGraph (MainScene)
' y se queda atendiendo el puerto de mensajes hasta que el usuario sale del canal.
sub Main(args as Dynamic)
    screen = CreateObject("roSGScreen")
    port = CreateObject("roMessagePort")
    screen.setMessagePort(port)

    scene = screen.CreateScene("MainScene")
    screen.show()

    ' Deep link: Roku puede lanzar el canal con un canal/stream concreto (mandos de voz,
    ' "Lanzar canal" desde otra app). Si llega algo útil en args, se lo pasamos a la escena.
    if args <> invalid and args.contentId <> invalid
        scene.deepLinkUrl = args.contentId
    end if

    while true
        msg = wait(0, port)
        msgType = type(msg)
        if msgType = "roSGScreenEvent"
            if msg.isScreenClosed()
                return
            end if
        end if
    end while
end sub
