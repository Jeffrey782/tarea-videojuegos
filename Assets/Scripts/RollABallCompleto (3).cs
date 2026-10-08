using System;
using System.Collections.Generic;
using UnityEngine;

// Roll-a-ball completo - Unity 6.6
// Jeffrey Fernandez | 1-21-1476
// Colocar este script en un GameObject vacio llamado GameManager.
public class RollABallCompleto : MonoBehaviour
{
    const string Estudiante = "Jeffrey Fernandez";
    const string Matricula = "1-21-1476";
    const float LimiteTiempo = 60f;
    enum Pantalla { Menu, Controles, Jugando, Ganado, Perdido, Final }
    Pantalla pantalla = Pantalla.Menu;
    readonly List<GameObject> creados = new List<GameObject>();
    readonly List<Transform> coleccionables = new List<Transform>();
    readonly List<ObstaculoMovil> moviles = new List<ObstaculoMovil>();
    readonly List<Transform> rotadores = new List<Transform>();
    readonly HashSet<int> recogidos = new HashSet<int>();
    Vector3 posicionInicial;
    GameObject jugador;
    Rigidbody cuerpo;
    Camera camara;
    [Header("Musica de fondo (arrastra aqui el archivo MP3)")]
    public AudioClip musicaAscensor;
    [Range(0f, 1f)] public float volumenMusica = 0.35f;
    AudioSource musica;
    Material sueloMat, paredMat, oroMat, plataMat, jugadorMat, peligroMat, metaMat;
    int nivel = 1;
    int total = 0;
    float tiempo = LimiteTiempo;
    float tiempoPantalla = 0f;
    float tiempoInicio = 0f;
    bool terminado = false;
    GUIStyle titulo, texto, boton, etiqueta, centrado;
    Texture2D fondoBoton;

    class ObstaculoMovil
    {
        public Transform objeto;
        public Vector3 centro;
        public Vector3 eje;
        public float distancia;
        public float velocidad;
        public float fase;
    }

    void Awake()
    {
        sueloMat = CrearMaterial(new Color(0.10f, 0.22f, 0.46f));
        paredMat = CrearMaterial(new Color(0.12f, 0.30f, 0.55f));
        oroMat = CrearMaterial(new Color(1f, 0.77f, 0.08f));
        plataMat = CrearMaterial(new Color(0.70f, 0.86f, 0.95f));
        jugadorMat = CrearMaterial(Color.white);
        peligroMat = CrearMaterial(new Color(0.88f, 0.16f, 0.16f));
        metaMat = CrearMaterial(new Color(0.20f, 0.78f, 0.37f));
        ConfigurarCamara();
        ConfigurarLuz();
        ConfigurarMusica();
        ConstruirMenuDecorativo();
    }

    Material CrearMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        mat.color = color;
        return mat;
    }

    GameObject Primitiva(PrimitiveType tipo, string nombre, Vector3 posicion, Vector3 escala, Material mat)
    {
        GameObject obj = GameObject.CreatePrimitive(tipo);
        obj.name = nombre;
        obj.transform.position = posicion;
        obj.transform.localScale = escala;
        Renderer r = obj.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = mat;
        creados.Add(obj);
        return obj;
    }

    void ConfigurarCamara()
    {
        camara = Camera.main;
        if (camara == null)
        {
            GameObject go = new GameObject("Camara Principal");
            camara = go.AddComponent<Camera>();
            go.tag = "MainCamera";
        }
        camara.orthographic = true;
        camara.orthographicSize = 14f;
        camara.transform.position = new Vector3(0, 19, -16);
        camara.transform.rotation = Quaternion.Euler(50, 0, 0);
        camara.backgroundColor = new Color(0.64f, 0.77f, 0.90f);
        camara.clearFlags = CameraClearFlags.SolidColor;
    }

    void ConfigurarLuz()
    {
        if (FindFirstObjectByType<Light>() != null) return;
        GameObject go = new GameObject("Luz Principal");
        Light luz = go.AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.intensity = 1.4f;
        go.transform.rotation = Quaternion.Euler(50, -30, 0);
    }

    void ConfigurarMusica()
    {
        musica = GetComponent<AudioSource>();
        if (musica == null) musica = gameObject.AddComponent<AudioSource>();
        musica.playOnAwake = false;
        musica.loop = true;
        musica.spatialBlend = 0f; // 2D: siempre audible
        musica.volume = volumenMusica;
        musica.clip = musicaAscensor;
        if (musicaAscensor != null)
            musica.Play();
        else
            Debug.LogWarning("RollABall: falta asignar el MP3 al campo Musica Ascensor del GameManager.");
    }

    void LimpiarNivel()
    {
        for (int i = 0; i < creados.Count; i++) if (creados[i] != null) Destroy(creados[i]);
        creados.Clear();
        coleccionables.Clear();
        rotadores.Clear();
        moviles.Clear();
        recogidos.Clear();
        jugador = null;
        cuerpo = null;
    }

    void ConstruirMenuDecorativo()
    {
        LimpiarNivel();
        Primitiva(PrimitiveType.Plane, "Suelo de presentacion", Vector3.zero, new Vector3(2, 1, 2), sueloMat);
        Primitiva(PrimitiveType.Sphere, "Esfera de presentacion", new Vector3(0, .55f, 0), Vector3.one, jugadorMat);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            GameObject o = Primitiva(PrimitiveType.Cylinder, "Coleccionable decorativo", new Vector3(Mathf.Cos(a) * 6f, .65f, Mathf.Sin(a) * 6f), new Vector3(.6f, .25f, .6f), oroMat);
            rotadores.Add(o.transform);
        }
    }

    void ComenzarNivel(int numero)
    {
        nivel = numero;
        pantalla = Pantalla.Jugando;
        terminado = false;
        tiempo = LimiteTiempo;
        tiempoInicio = Time.time;
        LimpiarNivel();
        Primitiva(PrimitiveType.Plane, "Suelo", Vector3.zero, new Vector3(2, 1, 2), sueloMat);
        // Bordes del tablero 20x20
        Muro("Pared norte", 0, 10, 20.5f, .5f);
        Muro("Pared sur", 0, -10, 20.5f, .5f);
        Muro("Pared este", 10, 0, .5f, 20.5f);
        Muro("Pared oeste", -10, 0, .5f, 20.5f);
        Vector3 inicio = new Vector3(0, .6f, 0);
        if (nivel == 3) inicio = new Vector3(-8.5f, .6f, -8.5f);
        if (nivel == 5) inicio = new Vector3(-8f, .6f, -8f);
        if (nivel == 6) inicio = new Vector3(-8f, .6f, -8f);
        posicionInicial = inicio;
        jugador = Primitiva(PrimitiveType.Sphere, "Jugador", inicio, Vector3.one, jugadorMat);
        cuerpo = jugador.AddComponent<Rigidbody>();
        cuerpo.mass = 1f;
        cuerpo.linearDamping = 1.8f;
        cuerpo.angularDamping = .5f;
        cuerpo.collisionDetectionMode = CollisionDetectionMode.Continuous;
        switch (nivel)
        {
            case 1: NivelUno(); break;
            case 2: NivelDos(); break;
            case 3: NivelLaberinto(); break;
            case 4: NivelMoviles(); break;
            case 5: NivelPrecision(); break;
            case 6: NivelFinal(); break;
        }
        total = coleccionables.Count;
    }

    void Muro(string nombre, float x, float z, float ancho, float largo)
    {
        Primitiva(PrimitiveType.Cube, nombre, new Vector3(x, .85f, z), new Vector3(ancho, 1.7f, largo), paredMat);
    }

    void Coleccionable(float x, float z, bool barra = false)
    {
        // Las barras son cilindros alargados y acostados: no se usan cubos coleccionables.
        GameObject obj = Primitiva(PrimitiveType.Cylinder, barra ? "Barra coleccionable" : "Cilindro coleccionable",
            new Vector3(x, .65f, z), barra ? new Vector3(.24f, .8f, .24f) : new Vector3(.62f, .22f, .62f),
            barra ? plataMat : oroMat);
        if (barra) obj.transform.rotation = Quaternion.Euler(0, 0, 90);
        Collider col = obj.GetComponent<Collider>();
        col.isTrigger = true;
        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        coleccionables.Add(obj.transform);
        rotadores.Add(obj.transform);
    }

    void Peligro(float x, float z, float ancho = 1.3f, float largo = 1.3f)
    {
        Primitiva(PrimitiveType.Cube, "Obstaculo rojo", new Vector3(x, .7f, z), new Vector3(ancho, 1.4f, largo), peligroMat);
    }

    void MoverObstaculo(float x, float z, Vector3 eje, float distancia, float velocidad, float fase = 0)
    {
        GameObject obj = Primitiva(PrimitiveType.Cube, "Obstaculo movil rojo", new Vector3(x, .7f, z), new Vector3(1.25f, 1.4f, 1.25f), peligroMat);
        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        moviles.Add(new ObstaculoMovil { objeto = obj.transform, centro = obj.transform.position, eje = eje.normalized, distancia = distancia, velocidad = velocidad, fase = fase });
    }

    void NivelUno()
    {
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            Coleccionable(Mathf.Cos(a) * 6.8f, Mathf.Sin(a) * 6.8f, i % 3 == 0);
        }
    }

    void NivelDos()
    {
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2f / 10f;
            Coleccionable(Mathf.Cos(a) * 7.7f, Mathf.Sin(a) * 7.7f, i % 2 == 0);
        }
        Peligro(-3, -3); Peligro(3, -3); Peligro(-3, 3); Peligro(3, 3);
        Muro("Barrera central", 0, 0, 1.1f, 3f);
    }

    void NivelLaberinto()
    {
        // Pasillos amplios, con entradas alternadas a izquierda y derecha.
        for (int i = 0; i < 5; i++)
        {
            float z = -6.5f + i * 3.2f;
            bool abrirDerecha = i % 2 == 0;
            float centroX = abrirDerecha ? -2.3f : 2.3f;
            Muro("Laberinto fila " + i, centroX, z, 14f, .45f);
            Coleccionable(abrirDerecha ? 7.9f : -7.9f, z + 1.3f, i % 2 == 0);
        }
        Coleccionable(0, 8.4f, true);
    }

    void NivelMoviles()
    {
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            Coleccionable(Mathf.Cos(a) * 7.8f, Mathf.Sin(a) * 7.8f, i % 3 == 0);
        }
        MoverObstaculo(-3, 0, Vector3.forward, 4.5f, 1.3f);
        MoverObstaculo(3, 0, Vector3.forward, 4.5f, 1.7f, 1.2f);
        MoverObstaculo(0, 3, Vector3.right, 5f, 1.2f, 2f);
    }

    void NivelPrecision()
    {
        // Ruta en zigzag delimitada por muros. El jugador no puede atravesarlos.
        Muro("Pasillo A izquierda", -6, -4, .45f, 11f);
        Muro("Pasillo A derecha", -3, -4, .45f, 11f);
        Muro("Pasillo B inferior", 1, 1, 8f, .45f);
        Muro("Pasillo B superior", 1, 4, 8f, .45f);
        Coleccionable(-8, -8); Coleccionable(-8, 0, true);
        Coleccionable(-4.5f, -8); Coleccionable(-4.5f, 0, true);
        Coleccionable(0, 2.5f); Coleccionable(4, 2.5f, true);
        Coleccionable(7.8f, 7.8f); Coleccionable(-7.8f, 7.8f, true);
        Peligro(5, -5, 1.5f, 1.5f);
    }

    void NivelFinal()
    {
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f;
            Coleccionable(Mathf.Cos(a) * 7.7f, Mathf.Sin(a) * 7.7f, i % 2 == 0);
        }
        Muro("Barrera final 1", -4, -3, .5f, 8f);
        Muro("Barrera final 2", 4, 3, .5f, 8f);
        Muro("Barrera final 3", 0, 0, 4f, .5f);
        MoverObstaculo(-6, 2, Vector3.forward, 4f, 1.5f);
        MoverObstaculo(6, -2, Vector3.forward, 4f, 1.7f, 1f);
        MoverObstaculo(0, -6, Vector3.right, 4f, 1.6f, 2f);
    }

    void Update()
    {
        if (pantalla == Pantalla.Jugando)
        {
            tiempo -= Time.deltaTime;
            if (tiempo <= 0f) { tiempo = 0f; Perder(); return; }
            if (jugador != null && jugador.transform.position.y < -3f) { Perder(); return; }
            DetectarColeccionables();
            DetectarObstaculos();
            if (camara != null && jugador != null)
            {
                Vector3 objetivo = new Vector3(jugador.transform.position.x * .18f, 19, -16 + jugador.transform.position.z * .18f);
                camara.transform.position = Vector3.Lerp(camara.transform.position, objetivo, Time.deltaTime * 2f);
            }
        }
        if (pantalla == Pantalla.Ganado || pantalla == Pantalla.Final)
        {
            tiempoPantalla -= Time.deltaTime;
            if (tiempoPantalla <= 0f)
            {
                if (pantalla == Pantalla.Final) IrMenu();
                else ComenzarNivel(nivel + 1);
            }
        }
        if (pantalla == Pantalla.Jugando || pantalla == Pantalla.Menu || pantalla == Pantalla.Controles)
        {
            for (int i = 0; i < rotadores.Count; i++)
                if (rotadores[i] != null && rotadores[i].gameObject.activeSelf)
                    rotadores[i].Rotate(Vector3.up, 85f * Time.deltaTime, Space.World);
        }
    }

    void FixedUpdate()
    {
        if (pantalla != Pantalla.Jugando || cuerpo == null) return;
        float h = 0, v = 0;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1;
        Vector3 direccion = new Vector3(h, 0, v).normalized;
        cuerpo.AddForce(direccion * 22f, ForceMode.Force);
        cuerpo.linearVelocity = Vector3.ClampMagnitude(cuerpo.linearVelocity, 9f);
        for (int i = 0; i < moviles.Count; i++)
        {
            ObstaculoMovil o = moviles[i];
            if (o.objeto == null) continue;
            Vector3 destino = o.centro + o.eje * Mathf.Sin((Time.time - tiempoInicio) * o.velocidad + o.fase) * o.distancia;
            Rigidbody rb = o.objeto.GetComponent<Rigidbody>();
            if (rb != null) rb.MovePosition(destino);
        }
    }

    void DetectarColeccionables()
    {
        if (jugador == null) return;
        Vector3 pos = jugador.transform.position;
        for (int i = 0; i < coleccionables.Count; i++)
        {
            Transform item = coleccionables[i];
            if (item == null || recogidos.Contains(i)) continue;
            Vector3 diferencia = item.position - pos;
            diferencia.y = 0;
            if (diferencia.sqrMagnitude < 1.05f * 1.05f)
            {
                recogidos.Add(i);
                item.gameObject.SetActive(false);
                if (recogidos.Count >= total) { Ganar(); return; }
            }
        }
    }

    void DetectarObstaculos()
    {
        if (jugador == null) return;
        Vector3 pos = jugador.transform.position;
        for (int i = 0; i < creados.Count; i++)
        {
            GameObject obj = creados[i];
            if (obj == null || !obj.name.Contains("Obstaculo")) continue;
            Vector3 d = obj.transform.position - pos;
            if (Mathf.Abs(d.x) < obj.transform.localScale.x * .5f + .43f &&
                Mathf.Abs(d.z) < obj.transform.localScale.z * .5f + .43f &&
                Mathf.Abs(d.y) < 1.5f) { ReiniciarIntento(); return; }
        }
    }

    // Al tocar un obstaculo rojo, reinicia el intento sin reiniciar el reloj.
    void ReiniciarIntento()
    {
        if (pantalla != Pantalla.Jugando || jugador == null) return;

        // Restaurar todos los coleccionables del nivel.
        recogidos.Clear();
        foreach (Transform item in coleccionables)
            if (item != null) item.gameObject.SetActive(true);

        // Detener la esfera y devolverla al inicio del nivel.
        if (cuerpo != null)
        {
            cuerpo.linearVelocity = Vector3.zero;
            cuerpo.angularVelocity = Vector3.zero;
            cuerpo.position = posicionInicial;
            cuerpo.rotation = Quaternion.identity;
            cuerpo.Sleep();
        }
        jugador.transform.SetPositionAndRotation(posicionInicial, Quaternion.identity);

        // No modificar 'tiempo' ni 'tiempoInicio': el cronometro sigue.
    }

    void Ganar()
    {
        if (terminado) return;
        terminado = true;
        if (cuerpo != null) cuerpo.isKinematic = true;
        pantalla = nivel == 6 ? Pantalla.Final : Pantalla.Ganado;
        tiempoPantalla = 5f;
    }

    void Perder()
    {
        if (terminado) return;
        terminado = true;
        if (cuerpo != null) cuerpo.isKinematic = true;
        pantalla = Pantalla.Perdido;
    }

    void IrMenu()
    {
        pantalla = Pantalla.Menu;
        nivel = 1;
        camara.transform.position = new Vector3(0, 19, -16);
        ConstruirMenuDecorativo();
    }

    // Interfaz redisenada: paneles opacos y texto de alto contraste.
    Texture2D botonHover;
    GUIStyle subtituloUI, pequenoUI, botonSecundario;

    void PrepararEstilos()
    {
        if (titulo != null) return;
        titulo = new GUIStyle(GUI.skin.label) { fontSize = 51, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titulo.normal.textColor = Color.white;
        texto = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        texto.normal.textColor = new Color(.77f, .86f, .96f);
        etiqueta = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        etiqueta.normal.textColor = Color.white;
        centrado = new GUIStyle(etiqueta) { alignment = TextAnchor.MiddleCenter, fontSize = 32 };
        centrado.normal.textColor = new Color(.30f, .91f, 1f);
        subtituloUI = new GUIStyle(texto) { fontSize = 16 };
        pequenoUI = new GUIStyle(texto) { fontSize = 15 };
        boton = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0) };
        fondoBoton = TexturaColor(new Color(.06f, .66f, .87f));
        botonHover = TexturaColor(new Color(.11f, .81f, 1f));
        boton.normal.background = fondoBoton;
        boton.hover.background = botonHover;
        boton.active.background = botonHover;
        boton.focused.background = fondoBoton;
        boton.normal.textColor = Color.white;
        boton.hover.textColor = Color.white;
        boton.active.textColor = Color.white;
        boton.focused.textColor = Color.white;
        botonSecundario = new GUIStyle(boton);
        botonSecundario.normal.background = TexturaColor(new Color(.14f, .23f, .38f));
        botonSecundario.hover.background = botonHover;
    }

    Texture2D TexturaColor(Color color)
    {
        Texture2D t = new Texture2D(1, 1);
        t.SetPixel(0, 0, color);
        t.Apply();
        return t;
    }

    void RectColor(Rect rect, Color color)
    {
        Color previo = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previo;
    }

    void FondoPantalla(float w, float h)
    {
        RectColor(new Rect(0, 0, w, h), new Color(.035f, .075f, .14f, 1f));
        RectColor(new Rect(0, 0, 9, h), new Color(.06f, .70f, .90f));
        RectColor(new Rect(0, h - 7, w, 7), new Color(.06f, .70f, .90f));
        RectColor(new Rect(w * .5f - 300, 115, 600, 3), new Color(.09f, .60f, .80f));
    }

    void Cabecera(float w)
    {
        GUI.Label(new Rect(w - 330, 15, 310, 25), Estudiante, etiqueta);
        GUI.Label(new Rect(w - 330, 42, 310, 22), "Matricula: " + Matricula, pequenoUI);
    }

    void OnGUI()
    {
        PrepararEstilos();
        // Resolucion virtual: todos los elementos mantienen sus proporciones.
        float escala = Mathf.Min(Screen.width / 1000f, Screen.height / 600f);
        Matrix4x4 anterior = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(escala, escala, 1f));
        float w = Screen.width / escala;
        float h = Screen.height / escala;
        float cx = w * .5f, cy = h * .5f;

        if (pantalla == Pantalla.Menu || pantalla == Pantalla.Controles)
        {
            FondoPantalla(w, h);
            Cabecera(w);
            if (pantalla == Pantalla.Menu)
            {
                GUI.Label(new Rect(cx - 360, cy - 215, 720, 85), "ROLL-A-BALL", titulo);
                GUI.Label(new Rect(cx - 340, cy - 125, 680, 42), "6 niveles  /  60 segundos  /  una gran aventura", texto);
                RectColor(new Rect(cx - 218, cy - 52, 436, 238), new Color(.075f, .14f, .24f));
                if (GUI.Button(new Rect(cx - 180, cy - 30, 360, 57), "JUGAR", boton)) ComenzarNivel(1);
                if (GUI.Button(new Rect(cx - 180, cy + 42, 360, 57), "CONTROLES", botonSecundario)) pantalla = Pantalla.Controles;
                if (GUI.Button(new Rect(cx - 180, cy + 114, 360, 57), "SALIR", botonSecundario)) Application.Quit();
                GUI.Label(new Rect(cx - 320, h - 60, 640, 30), "Recoge los objetos dorados y evita los bloques rojos", pequenoUI);
            }
            else
            {
                GUI.Label(new Rect(cx - 350, cy - 220, 700, 80), "CONTROLES", titulo);
                RectColor(new Rect(cx - 290, cy - 115, 580, 275), new Color(.075f, .14f, .24f));
                GUI.Label(new Rect(cx - 265, cy - 94, 530, 215),
                    "W / Flecha arriba: avanzar\nS / Flecha abajo: retroceder\nA y D / Flechas: izquierda y derecha\nRecoge todos los cilindros y barras\nRojo = vuelves al inicio, sin recuperar tiempo", texto);
                if (GUI.Button(new Rect(cx - 170, cy + 180, 340, 58), "VOLVER AL MENU", boton)) pantalla = Pantalla.Menu;
            }
        }
        else if (pantalla == Pantalla.Jugando)
        {
            RectColor(new Rect(14, 14, 260, 114), new Color(.035f, .075f, .14f, .96f));
            RectColor(new Rect(14, 14, 6, 114), new Color(.06f, .75f, .94f));
            GUI.Label(new Rect(32, 18, 225, 28), "NIVEL " + nivel + " / 6", etiqueta);
            GUI.Label(new Rect(32, 51, 225, 28), "Objetos: " + recogidos.Count + " / " + total, etiqueta);
            GUI.Label(new Rect(32, 84, 225, 28), "Tiempo: " + Mathf.CeilToInt(tiempo) + " s", etiqueta);
            Cabecera(w);
        }
        else
        {
            FondoPantalla(w, h);
            Cabecera(w);
            bool derrota = pantalla == Pantalla.Perdido;
            bool finalJuego = pantalla == Pantalla.Final;
            string mensaje = derrota ? "TIEMPO AGOTADO" : finalJuego ? "¡JUEGO COMPLETADO!" : "¡NIVEL COMPLETADO!";
            GUI.Label(new Rect(cx - 460, cy - 195, 920, 90), mensaje, titulo);
            RectColor(new Rect(cx - 315, cy - 77, 630, 225), new Color(.075f, .14f, .24f));
            if (derrota)
            {
                GUI.Label(new Rect(cx - 285, cy - 50, 570, 55), "No lograste recoger todos los objetos a tiempo", texto);
                if (GUI.Button(new Rect(cx - 265, cy + 40, 250, 64), "REINTENTAR", boton)) ComenzarNivel(nivel);
                if (GUI.Button(new Rect(cx + 15, cy + 40, 250, 64), "MENU", botonSecundario)) IrMenu();
            }
            else
            {
                GUI.Label(new Rect(cx - 285, cy - 46, 570, 42), finalJuego ? "¡Completaste los seis niveles!" : "Preparando el siguiente nivel...", texto);
                GUI.Label(new Rect(cx - 220, cy + 10, 440, 75), Mathf.CeilToInt(tiempoPantalla) + " s", centrado);
                GUI.Label(new Rect(cx - 285, cy + 94, 570, 30), finalJuego ? "Volviendo al menu principal" : "Siguiente desafio en breve", pequenoUI);
            }
        }
        GUI.matrix = anterior;
    }

    void OnDestroy()
    {
        if (fondoBoton != null) Destroy(fondoBoton);
        if (botonHover != null) Destroy(botonHover);
        if (botonSecundario != null && botonSecundario.normal.background != null) Destroy(botonSecundario.normal.background);
    }
}
