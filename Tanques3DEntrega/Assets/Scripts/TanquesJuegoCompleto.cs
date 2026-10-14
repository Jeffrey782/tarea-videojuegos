using System.Collections.Generic;
using UnityEngine;

// Coloca este archivo en Assets/Scripts/TanquesJuegoCompleto.cs
// Agrega el componente a un GameObject vacio llamado GameManager.
public class TanquesJuegoCompleto : MonoBehaviour
{
    [Header("Audio (asignar en Inspector)")]
    public AudioClip marchaFunebre;
    public AudioClip sonidoDisparo;
    public AudioClip sonidoExplosion;

    [Header("Reglas")]
    public float duracionPartida = 240f;
    public int derrotasMaximas = 5;
    public int cantidadEnemigos = 3;

    [Header("Modelos opcionales estilo low-poly (arrastrar prefabs)")]
    public GameObject modeloTanqueAzul;
    public GameObject modeloTanqueRojo;
    public GameObject modeloRoca;
    public GameObject modeloArbolSeco;
    public GameObject modeloEdificio;

    enum Estado { Menu, Jugando, Final }
    Estado estado = Estado.Menu;

    class Tanque
    {
        public GameObject objeto;
        public Rigidbody cuerpo;
        public Transform canon;
        public int salud = 100;
        public int derrotas;
        public int victorias;
        public float siguienteDisparo;
        public Vector3 inicio;
        public Quaternion giroInicial;
        public bool vivo = true;
    }

    class Enemigo
    {
        public GameObject objeto;
        public Rigidbody cuerpo;
        public float siguienteCambio;
        public float siguienteAtaque;
        public Vector3 destino;
    }

    class Bala
    {
        public GameObject objeto;
        public Vector3 direccion;
        public float vida;
        public int propietario; // 0, 1 o -1 (enemigo)
    }

    readonly List<GameObject> creados = new List<GameObject>();
    readonly List<Enemigo> enemigos = new List<Enemigo>();
    readonly List<Bala> balas = new List<Bala>();
    Tanque[] jugadores = new Tanque[2];
    Camera camara;
    AudioSource musica;
    AudioSource efectos;
    float restante;
    float transcurrido;
    string resultado = "";
    GUIStyle titulo, texto, boton, centro, pequeno;
    Texture2D fondoGUI;
    bool interfazLista;
    int ronda = 1;
    float siguienteRonda;
    bool esperandoRonda;

    void Awake()
    {
        Application.targetFrameRate = 60;
        musica = gameObject.AddComponent<AudioSource>();
        musica.playOnAwake = false;
        musica.loop = true;
        musica.spatialBlend = 0f;
        musica.volume = 0.45f;
        efectos = gameObject.AddComponent<AudioSource>();
        efectos.playOnAwake = false;
        efectos.spatialBlend = 0f;
        // Carga automatica de los FBX incluidos: no requiere arrastrar prefabs.
        if (modeloRoca == null) modeloRoca = Resources.Load<GameObject>("Kenney/Naturaleza/rock_largeD");
        if (modeloArbolSeco == null) modeloArbolSeco = Resources.Load<GameObject>("Kenney/Naturaleza/cactus_tall");
        if (modeloEdificio == null) modeloEdificio = Resources.Load<GameObject>("Kenney/Ciudad/building-type-a");
        if (marchaFunebre == null) marchaFunebre = Resources.Load<AudioClip>("Audio/Marcha_Funebre_Original");
        if (sonidoDisparo == null) sonidoDisparo = Resources.Load<AudioClip>("Audio/Disparo_Tanque");
        if (sonidoExplosion == null) sonidoExplosion = Resources.Load<AudioClip>("Audio/Explosion_Tanque");
        PrepararCamara();
    }

    void PrepararCamara()
    {
        camara = Camera.main;
        if (camara == null)
        {
            GameObject cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            camara = cam.AddComponent<Camera>();
        }
        camara.orthographic = true;
        camara.transform.position = new Vector3(0f, 41f, -43f);
        camara.transform.rotation = Quaternion.Euler(46f, 0f, 0f);
        camara.clearFlags = CameraClearFlags.SolidColor;
        camara.backgroundColor = new Color(0.96f, 0.73f, 0.43f);
        AjustarCamara();
        if (FindFirstObjectByType<Light>() == null)
        {
            GameObject sol = new GameObject("Luz principal");
            Light l = sol.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.3f;
            sol.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    void AjustarCamara()
    {
        if (camara == null) return;
        float aspecto = (float)Screen.width / Mathf.Max(1, Screen.height);
        // Margen para mostrar el terreno y los cuatro bordes en WebGL.
        camara.orthographicSize = Mathf.Max(29f, 29f * (16f / 9f) / aspecto);
    }

    Material Material(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material m = new Material(shader);
        m.color = color;
        return m;
    }

    GameObject Cubo(string nombre, Vector3 posicion, Vector3 escala, Color color, Transform padre = null)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        if (padre != null) go.transform.SetParent(padre, false);
        go.transform.localPosition = posicion;
        go.transform.localScale = escala;
        go.GetComponent<Renderer>().material = Material(color);
        creados.Add(go);
        return go;
    }

    void Decoracion(string nombre, Vector3 posicion, Vector3 escala, Color color,
                    Transform padre = null)
    {
        GameObject o = Cubo(nombre, posicion, escala, color, padre);
        Collider c = o.GetComponent<Collider>();
        if (c != null) c.enabled = false;
    }

    void LuzPuntual(string nombre, Vector3 posicion, Color color, float intensidad, float alcance)
    {
        GameObject o = new GameObject(nombre);
        creados.Add(o);
        o.transform.position = posicion;
        Light l = o.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensidad;
        l.range = alcance;
    }

    // Decoraciones sin colisiones, para no bloquear la partida.
    GameObject Forma(PrimitiveType tipo, string nombre, Vector3 pos, Vector3 escala,
                     Color color, Transform padre = null)
    {
        GameObject o = GameObject.CreatePrimitive(tipo);
        o.name = nombre;
        if (padre != null) o.transform.SetParent(padre, false);
        o.transform.localPosition = pos;
        o.transform.localScale = escala;
        Renderer r = o.GetComponent<Renderer>();
        if (r != null) r.material = Material(color);
        Collider c = o.GetComponent<Collider>();
        if (c != null) c.enabled = false;
        creados.Add(o);
        return o;
    }

    // Ajusta FBX de distintas unidades de escala a tamanos visibles en el mundo.
    void ColocarDecoracion(GameObject prefab, Vector3 pos, float tamano, string nombre)
    {
        if (prefab == null) return;
        GameObject o = Instantiate(prefab, Vector3.zero, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
        o.name = nombre;
        foreach (Collider c in o.GetComponentsInChildren<Collider>()) c.enabled = false;
        Renderer[] renders = o.GetComponentsInChildren<Renderer>();
        if (renders.Length == 0) { Destroy(o); return; }
        Bounds bounds = renders[0].bounds;
        for (int i = 1; i < renders.Length; i++) bounds.Encapsulate(renders[i].bounds);
        float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxDimension > 0.001f)
            o.transform.localScale *= Mathf.Clamp(tamano / maxDimension, 0.01f, 500f);
        // Recalcular bounds tras escalar y apoyar el modelo en la arena.
        renders = o.GetComponentsInChildren<Renderer>();
        bounds = renders[0].bounds;
        for (int i = 1; i < renders.Length; i++) bounds.Encapsulate(renders[i].bounds);
        o.transform.position = pos + new Vector3(0f, -bounds.min.y, 0f);
        Color tinte = nombre.Contains("Roca") ? new Color(.66f,.47f,.34f) :
                      nombre.Contains("Edificio") ? new Color(.85f,.73f,.58f) : new Color(.38f,.52f,.28f);
        foreach (Renderer r in renders)
        {
            Material[] mats = r.sharedMaterials;
            for (int j = 0; j < mats.Length; j++) mats[j] = Material(tinte);
            r.materials = mats;
        }
        creados.Add(o);
    }

    void Roca(Vector3 pos, float tamano)
    {
        if (modeloRoca != null) { ColocarDecoracion(modeloRoca, pos, tamano, "Roca low-poly"); return; }
        Color[] tonos = {new Color(.48f,.32f,.23f), new Color(.58f,.38f,.25f), new Color(.67f,.47f,.31f)};
        for (int i = 0; i < 3; i++)
        {
            GameObject o = Forma(PrimitiveType.Cube, "Roca facetada", pos + new Vector3(i*.75f, .6f, i*.25f),
                new Vector3(tamano*(1f-i*.18f), tamano*(1.1f-i*.2f), tamano*(.85f+i*.08f)), tonos[i]);
            o.transform.rotation = Quaternion.Euler(i*12f, i*31f, i*17f);
        }
    }

    void ArbolSeco(Vector3 pos, float altura)
    {
        if (modeloArbolSeco != null) { ColocarDecoracion(modeloArbolSeco, pos, altura/5f, "Arbol seco low-poly"); return; }
        Color madera = new Color(.30f,.20f,.14f);
        GameObject tronco = Forma(PrimitiveType.Cylinder,"Tronco", pos + Vector3.up*altura*.42f,
            new Vector3(.20f, altura*.43f, .20f), madera);
        tronco.transform.rotation = Quaternion.Euler(0,0,8);
        for(int i=0;i<4;i++)
        {
            GameObject rama = Forma(PrimitiveType.Cylinder,"Rama seca",
                pos + new Vector3(0,altura*(.54f+i*.10f),0), new Vector3(.09f,altura*.23f,.09f),madera);
            rama.transform.rotation = Quaternion.Euler(i*17f, i*75f, i%2==0 ? 43f : -43f);
        }
    }

    void Edificio(Vector3 pos, float ancho)
    {
        if (modeloEdificio != null) { ColocarDecoracion(modeloEdificio,pos,ancho/5f,"Edificio desierto"); return; }
        Color pared = new Color(.69f,.59f,.48f);
        Forma(PrimitiveType.Cube,"Casa desierto",pos+Vector3.up*2.5f,new Vector3(ancho,5f,ancho*.75f),pared);
        Forma(PrimitiveType.Cube,"Tejado",pos+Vector3.up*5.15f,new Vector3(ancho+.5f,.4f,ancho*.9f),new Color(.42f,.38f,.34f));
        Forma(PrimitiveType.Cube,"Puerta",pos+new Vector3(0,1.3f,-ancho*.39f),new Vector3(1.3f,2.6f,.12f),new Color(.30f,.27f,.23f));
        Forma(PrimitiveType.Cube,"Ventana",pos+new Vector3(-ancho*.32f,3.2f,-ancho*.39f),new Vector3(.85f,.8f,.12f),new Color(.19f,.24f,.26f));
    }

    void Anillo(Vector3 pos, Color color)
    {
        // Anillo de 20 segmentos, pegado al suelo y sin colisiones.
        for(int i=0;i<20;i++)
        {
            float a=i*Mathf.PI*2f/20f;
            GameObject o=Forma(PrimitiveType.Cube,"Marcador de jugador",
                pos + new Vector3(Mathf.Cos(a)*2.4f,-.77f,Mathf.Sin(a)*2.4f),
                new Vector3(.75f,.055f,.33f),color);
            o.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
        }
    }

    void ConstruirMundo()
    {
        Cubo("Desierto extendido", new Vector3(0, -0.55f, 0), new Vector3(220, 1, 220), new Color(1f, 0.76f, 0.45f));
        Color pared = new Color(0.72f, 0.52f, 0.33f);
        Cubo("Pared Norte", new Vector3(0, 1, 25), new Vector3(51, 2, 1), pared);
        Cubo("Pared Sur", new Vector3(0, 1, -25), new Vector3(51, 2, 1), pared);
        Cubo("Pared Este", new Vector3(25, 1, 0), new Vector3(1, 2, 51), pared);
        Cubo("Pared Oeste", new Vector3(-25, 1, 0), new Vector3(1, 2, 51), pared);
        // Los limites siguen bloqueando tanques y proyectiles, pero no se ve el marco cuadrado.
        foreach (GameObject objeto in creados)
            if (objeto != null && objeto.name.StartsWith("Pared "))
            {
                Renderer render = objeto.GetComponent<Renderer>();
                if (render != null) render.enabled = false;
            }

        Color obstaculo = new Color(0.58f, 0.43f, 0.31f);
        Cubo("Cobertura A", new Vector3(-9, 1.1f, -7), new Vector3(3.5f, 2.2f, 3), obstaculo);
        Cubo("Cobertura B", new Vector3(9, 1.1f, 7), new Vector3(3.5f, 2.2f, 3), obstaculo);
        Cubo("Cobertura C", new Vector3(0, 1.1f, -12), new Vector3(3f, 2.2f, 3.5f), obstaculo);
        Cubo("Cobertura D", new Vector3(0, 1.1f, 12), new Vector3(3f, 2.2f, 3.5f), obstaculo);
        // Arena desertica abierta, con elementos escenicos fuera de las rutas principales.
        Roca(new Vector3(-18f,0f,17f), 2.8f);
        Roca(new Vector3(18f,0f,-17f), 2.4f);
        Roca(new Vector3(19f,0f,19f), 2.0f);
        ArbolSeco(new Vector3(-20f,0f,-14f),5.2f);
        ArbolSeco(new Vector3(19f,0f,13f),4.4f);
        ArbolSeco(new Vector3(-14f,0f,19f),4.7f);
        Edificio(new Vector3(-20f,0f,-20f),5f);
        Edificio(new Vector3(20f,0f,21f),4.5f);
        // Objetos adicionales de los paquetes gratuitos Kenney.
        GameObject rocaAlt = Resources.Load<GameObject>("Kenney/Naturaleza/rock_largeA");
        GameObject cactus = Resources.Load<GameObject>("Kenney/Naturaleza/cactus_short");
        GameObject palmera = Resources.Load<GameObject>("Kenney/Naturaleza/tree_palmShort");
        GameObject casaAlt = Resources.Load<GameObject>("Kenney/Ciudad/building-type-d");
        ColocarDecoracion(rocaAlt, new Vector3(-15,0,-19), 3.0f, "Roca adicional");
        ColocarDecoracion(rocaAlt, new Vector3(15,0,18), 2.4f, "Roca adicional");
        ColocarDecoracion(cactus, new Vector3(-21,0,5), 2.4f, "Cactus desierto");
        ColocarDecoracion(cactus, new Vector3(21,0,-6), 2.2f, "Cactus desierto");
        ColocarDecoracion(palmera, new Vector3(-22,0,21), 2.2f, "Palmera desierto");
        ColocarDecoracion(casaAlt, new Vector3(20,0,-20), 3.2f, "Edificio desierto");
        // Escenografia exterior: edificios y formaciones visibles en los bordes de la camara.
        Edificio(new Vector3(-31f, 0f, 12f), 8f);
        Edificio(new Vector3(31f, 0f, -9f), 8f);
        Edificio(new Vector3(-27f, 0f, -31f), 7f);
        Edificio(new Vector3(29f, 0f, 29f), 7f);
        Roca(new Vector3(8f, 0f, -27f), 6f);
        Roca(new Vector3(-7f, 0f, 28f), 6f);
        Roca(new Vector3(29f, 0f, 7f), 5f);
        ArbolSeco(new Vector3(-27f, 0f, -7f), 6f);
        ArbolSeco(new Vector3(27f, 0f, 15f), 6f);
        // Siluetas de dunas sin colision para que el suelo no parezca plano.
        for (int i = 0; i < 20; i++)
        {
            float ang = i * Mathf.PI * 2f / 20f;
            Vector3 dp = new Vector3(Mathf.Cos(ang) * 37f, -.28f, Mathf.Sin(ang) * 36f);
            GameObject duna = Forma(PrimitiveType.Sphere, "Duna lejana", dp,
                new Vector3(12f, .55f, 6f), new Color(.97f, .70f, .39f));
            duna.transform.rotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
        }
        // Pequenos parches de arena para dar relieve visual sin interferir con la fisica.
        Random.InitState(2026);
        for(int i=0;i<34;i++)
        {
            Vector3 p=new Vector3(Random.Range(-22f,22f),.012f,Random.Range(-22f,22f));
            if(p.magnitude<7f)continue;
            GameObject d=Forma(PrimitiveType.Cube,"Arena / marca",p,
                new Vector3(Random.Range(.4f,1.5f),.018f,Random.Range(1.5f,3.5f)),
                i%2==0?new Color(.98f,.73f,.42f):new Color(.88f,.58f,.31f));
            d.transform.rotation=Quaternion.Euler(0,Random.Range(0f,180f),0);
        }
        // Luz solar de tarde para sombras y color calido.
        Light sol=FindFirstObjectByType<Light>();
        if(sol!=null && sol.type==LightType.Directional)
        {
            sol.color=new Color(1f,.85f,.66f);
            sol.intensity=1.35f;
            sol.transform.rotation=Quaternion.Euler(48f,-28f,0f);
        }
        for (int i = 0; i < 2; i++)
        {
            Vector3 inicio = new Vector3(i == 0 ? -16 : 16, 0.85f, 0);
            jugadores[i] = CrearTanque(i, inicio);
        }
        for (int i = 0; i < Mathf.Clamp(cantidadEnemigos, 0, 10); i++)
        {
            Vector3 p = new Vector3((i - 1) * 8f, 0.8f, i % 2 == 0 ? 15f : -15f);
            enemigos.Add(CrearEnemigo(p));
        }
    }

    Tanque CrearTanque(int id, Vector3 posicion)
    {
        Color color = id == 0 ? new Color(0.12f, 0.49f, 0.95f) : new Color(0.94f, 0.19f, 0.20f);
        GameObject raiz = new GameObject(id == 0 ? "Tanque Azul - Jugador 1" : "Tanque Rojo - Jugador 2");
        creados.Add(raiz);
        raiz.transform.position = posicion;
        raiz.transform.rotation = Quaternion.Euler(0, id == 0 ? 90 : -90, 0);
        Rigidbody rb = raiz.AddComponent<Rigidbody>();
        rb.mass = 5f;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        BoxCollider col = raiz.AddComponent<BoxCollider>();
        col.size = new Vector3(2.2f, 1.5f, 3.2f);
        // Modelo estilizado inspirado en tanques low-poly de dibujos animados.
        Color sombra = Color.Lerp(color, Color.black, .30f);
        Color brillo = Color.Lerp(color, Color.white, .24f);
        Color goma = new Color(.12f,.14f,.17f);
        Color metal = new Color(.27f,.32f,.36f);
        Color plata = new Color(.63f,.68f,.70f);
        // Chasis redondeado con defensas y paneles inclinados.
        Forma(PrimitiveType.Cube,"Casco inferior",new Vector3(0,-.20f,0),new Vector3(2.18f,.65f,3.08f),sombra,raiz.transform);
        Forma(PrimitiveType.Cube,"Casco superior",new Vector3(0,.18f,-.08f),new Vector3(2.04f,.64f,2.62f),color,raiz.transform);
        Forma(PrimitiveType.Cube,"Frente blindado",new Vector3(0,.06f,1.40f),new Vector3(1.98f,.53f,.40f),brillo,raiz.transform).transform.localRotation=Quaternion.Euler(-20,0,0);
        Forma(PrimitiveType.Cube,"Parte trasera",new Vector3(0,.20f,-1.44f),new Vector3(1.90f,.49f,.32f),sombra,raiz.transform);
        // Orugas continuas con ruedas visibles a ambos lados.
        for(int lado=-1;lado<=1;lado+=2)
        {
            float x=lado*1.20f;
            Forma(PrimitiveType.Capsule,"Oruga de caucho",new Vector3(x,-.35f,0),new Vector3(.62f,.76f,1.60f),goma,raiz.transform).transform.localRotation=Quaternion.Euler(90,0,0);
            Forma(PrimitiveType.Cube,"Banda lateral",new Vector3(x,-.33f,0),new Vector3(.65f,.48f,2.72f),goma,raiz.transform);
            for(int k=0;k<5;k++)
            {
                float z=-1.12f+k*.56f;
                GameObject rueda=Forma(PrimitiveType.Cylinder,"Rueda oruga",new Vector3(x+lado*.37f,-.39f,z),new Vector3(.36f,.085f,.36f),metal,raiz.transform);
                rueda.transform.localRotation=Quaternion.Euler(0,0,90);
                GameObject centroRueda=Forma(PrimitiveType.Cylinder,"Centro rueda",new Vector3(x+lado*.47f,-.39f,z),new Vector3(.15f,.09f,.15f),plata,raiz.transform);
                centroRueda.transform.localRotation=Quaternion.Euler(0,0,90);
            }
            for(int k=0;k<9;k++)
                Decoracion("Relieve de oruga",new Vector3(x,-.65f,-1.27f+k*.32f),new Vector3(.70f,.13f,.085f),metal,raiz.transform);
            Decoracion("Guardabarros",new Vector3(lado*1.05f,.09f,0),new Vector3(.35f,.13f,3.00f),brillo,raiz.transform);
        }
        // Torreta redonda y alta; el cañon permanece vinculado a la logica de disparo.
        Forma(PrimitiveType.Cylinder,"Base de torreta",new Vector3(0,.58f,-.14f),new Vector3(.85f,.20f,.85f),sombra,raiz.transform);
        Forma(PrimitiveType.Sphere,"Torreta abovedada",new Vector3(0,.92f,-.20f),new Vector3(1.60f,.85f,1.72f),color,raiz.transform);
        Forma(PrimitiveType.Cylinder,"Escotilla circular",new Vector3(-.27f,1.35f,-.38f),new Vector3(.36f,.09f,.36f),sombra,raiz.transform);
        Forma(PrimitiveType.Cylinder,"Tapa escotilla",new Vector3(-.27f,1.43f,-.38f),new Vector3(.27f,.05f,.27f),brillo,raiz.transform);
        GameObject canon=Forma(PrimitiveType.Cylinder,"Canon",new Vector3(0,.94f,1.12f),new Vector3(.20f,1.03f,.20f),metal,raiz.transform);
        canon.transform.localRotation=Quaternion.Euler(90,0,0);
        Forma(PrimitiveType.Cylinder,"Boca del canon",new Vector3(0,.94f,2.12f),new Vector3(.28f,.12f,.28f),sombra,raiz.transform).transform.localRotation=Quaternion.Euler(90,0,0);
        Forma(PrimitiveType.Cube,"Visor frontal",new Vector3(.43f,1.00f,.51f),new Vector3(.29f,.20f,.12f),plata,raiz.transform);
        for(int lado=-1;lado<=1;lado+=2)
        {
            Forma(PrimitiveType.Sphere,"Faro redondo",new Vector3(lado*.71f,.14f,1.57f),new Vector3(.29f,.29f,.16f),new Color(.92f,.94f,.72f),raiz.transform);
            Forma(PrimitiveType.Cube,"Parachoques",new Vector3(lado*.65f,-.15f,1.69f),new Vector3(.55f,.13f,.14f),metal,raiz.transform);
        }
        // Antena de radio inclinada hacia atras.
        Forma(PrimitiveType.Cylinder,"Antena",new Vector3(-.56f,1.78f,-.72f),new Vector3(.025f,.67f,.025f),goma,raiz.transform).transform.localRotation=Quaternion.Euler(-12,0,-12);
        // Las piezas visuales no deben interferir con el collider principal.
        foreach(Collider pieza in raiz.GetComponentsInChildren<Collider>())
            if(pieza.gameObject!=raiz) pieza.enabled=false;
        // El anillo sigue al tanque, pero no gira ni bloquea nada.
        GameObject marcador = new GameObject("Marcador circular");
        marcador.transform.SetParent(raiz.transform, false);
        marcador.transform.localPosition=Vector3.zero;
        creados.Add(marcador);
        Color marcadorColor = id == 0 ? new Color(.15f,.95f,.24f) : new Color(.18f,.92f,.27f);
        for(int i=0;i<24;i++)
        {
            float a=i*Mathf.PI*2f/24f;
            GameObject seg=Forma(PrimitiveType.Cube,"Anillo",new Vector3(Mathf.Cos(a)*2.2f,-.78f,Mathf.Sin(a)*2.2f),
                new Vector3(.58f,.06f,.30f),marcadorColor,marcador.transform);
            seg.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
        }
        GameObject prefab = id==0 ? modeloTanqueAzul : modeloTanqueRojo;
        if(prefab!=null)
        {
            // Se oculta la carroceria geometrica original, manteniendo colision y fisica.
            foreach(Transform hijo in raiz.transform)
                if(hijo!=marcador.transform) hijo.gameObject.SetActive(false);
            GameObject visual=Instantiate(prefab,raiz.transform);
            visual.name="Modelo visual tanque";
            visual.transform.localPosition=Vector3.zero;
            visual.transform.localRotation=Quaternion.identity;
            foreach(Collider c in visual.GetComponentsInChildren<Collider>()) c.enabled=false;
            creados.Add(visual);
        }
        return new Tanque { objeto = raiz, cuerpo = rb, canon = canon.transform, inicio = posicion, giroInicial = raiz.transform.rotation };
    }

    Enemigo CrearEnemigo(Vector3 posicion)
    {
        GameObject raiz = new GameObject("Enemigo IA");
        creados.Add(raiz);
        raiz.transform.position = posicion;
        Rigidbody rb = raiz.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        BoxCollider col = raiz.AddComponent<BoxCollider>();
        col.size = new Vector3(2, 1.6f, 2);
        Cubo("Cuerpo Enemigo", Vector3.zero, new Vector3(2, 1.2f, 2), new Color(0.95f, 0.62f, 0.10f), raiz.transform).GetComponent<Collider>().enabled = false;
        Cubo("Parte Superior", new Vector3(0, 0.85f, 0), new Vector3(1.2f, 0.5f, 1.2f), Color.black, raiz.transform).GetComponent<Collider>().enabled = false;
        Decoracion("Visor enemigo", new Vector3(0, 1.18f, 0.6f),
            new Vector3(0.75f, 0.15f, 0.16f), new Color(1f, 0.18f, 0.12f), raiz.transform);
        Decoracion("Parachoques enemigo", new Vector3(0, 0.12f, 1.03f),
            new Vector3(1.8f, 0.26f, 0.16f), new Color(0.18f, 0.20f, 0.22f), raiz.transform);
        return new Enemigo { objeto = raiz, cuerpo = rb, destino = Vector3.zero, siguienteCambio = 0 };
    }

    void Iniciar()
    {
        Limpiar();
        jugadores = new Tanque[2];
        restante = duracionPartida;
        transcurrido = 0;
        ronda = 1;
        esperandoRonda = false;
        resultado = "";
        ConstruirMundo();
        estado = Estado.Jugando;
        if (marchaFunebre != null)
        {
            musica.clip = marchaFunebre;
            musica.Play();
        }
    }

    void VolverAlMenu()
    {
        // Destruye los elementos de la partida, conserva el GameManager y la camara.
        Limpiar();
        jugadores = new Tanque[2];
        esperandoRonda = false;
        ronda = 1;
        restante = duracionPartida;
        transcurrido = 0f;
        resultado = "";
        if (musica != null) musica.Stop();
        if (efectos != null) efectos.Stop();
        estado = Estado.Menu;
    }

    void Limpiar()
    {
        for (int i = 0; i < creados.Count; i++) if (creados[i] != null) Destroy(creados[i]);
        creados.Clear();
        balas.Clear();
        enemigos.Clear();
    }

    void Update()
    {
        AjustarCamara();
        if (estado != Estado.Jugando) return;
        float dt = Time.deltaTime;
        restante -= dt;
        transcurrido += dt;
        if (restante <= 0)
        {
            restante = 0;
            Finalizar("TIEMPO AGOTADO\nAMBOS JUGADORES PIERDEN");
            return;
        }
        if (esperandoRonda)
        {
            if (Time.time >= siguienteRonda) ReiniciarRonda();
            return;
        }
        ControlJugador(0, KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space, dt);
        ControlJugador(1, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.Return, dt);
        MoverEnemigos(dt);
        MoverBalas(dt);
        if (!jugadores[0].vivo || !jugadores[1].vivo) ResolverRonda();
    }

    void ControlJugador(int id, KeyCode adelante, KeyCode atras, KeyCode izquierda, KeyCode derecha, KeyCode disparar, float dt)
    {
        Tanque t = jugadores[id];
        if (!t.vivo) return;
        float mover = (Input.GetKey(adelante) ? 1f : 0f) - (Input.GetKey(atras) ? 1f : 0f);
        float girar = (Input.GetKey(derecha) ? 1f : 0f) - (Input.GetKey(izquierda) ? 1f : 0f);
        t.cuerpo.MoveRotation(t.cuerpo.rotation * Quaternion.Euler(0, girar * 115f * dt, 0));
        if (mover != 0)
            t.cuerpo.MovePosition(t.cuerpo.position + t.objeto.transform.forward * (mover * 10f * dt));
        if (Input.GetKey(disparar) && Time.time >= t.siguienteDisparo)
        {
            t.siguienteDisparo = Time.time + 0.55f;
            Disparar(t.objeto.transform.position + t.objeto.transform.forward * 2.5f + Vector3.up * 0.5f, t.objeto.transform.forward, id);
        }
    }

    void Disparar(Vector3 origen, Vector3 direccion, int propietario)
    {
        GameObject bola = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bola.name = "Proyectil";
        bola.transform.position = origen;
        bola.transform.localScale = Vector3.one * 0.7f;
        bola.GetComponent<Renderer>().material = Material(propietario == -1 ? Color.yellow : Color.white);
        Collider c = bola.GetComponent<Collider>();
        c.enabled = false;
        creados.Add(bola);
        balas.Add(new Bala { objeto = bola, direccion = direccion.normalized, vida = 2.7f, propietario = propietario });
        if (sonidoDisparo != null) efectos.PlayOneShot(sonidoDisparo, 0.5f);
    }

    void MoverBalas(float dt)
    {
        for (int i = balas.Count - 1; i >= 0; i--)
        {
            Bala b = balas[i];
            if (b.objeto == null) { balas.RemoveAt(i); continue; }
            float distancia = 22f * dt;
            RaycastHit golpe;
            bool impacto = Physics.SphereCast(b.objeto.transform.position, 0.25f, b.direccion, out golpe, distancia);
            if (impacto)
            {
                for (int j = 0; j < jugadores.Length; j++)
                {
                    Tanque t = jugadores[j];
                    if (t != null && t.vivo && (b.propietario == -1 || b.propietario != j) && golpe.collider.transform == t.objeto.transform)
                    {
                        t.salud -= b.propietario == -1 ? 15 : 35;
                        if (t.salud <= 0) { t.salud = 0; t.vivo = false; t.objeto.SetActive(false); if (sonidoExplosion != null) efectos.PlayOneShot(sonidoExplosion); }
                    }
                }
                Destroy(b.objeto);
                balas.RemoveAt(i);
                continue;
            }
            b.objeto.transform.position += b.direccion * distancia;
            b.vida -= dt;
            if (b.vida <= 0) { Destroy(b.objeto); balas.RemoveAt(i); }
        }
    }

    void MoverEnemigos(float dt)
    {
        for (int i = 0; i < enemigos.Count; i++)
        {
            Enemigo e = enemigos[i];
            if (e.objeto == null) continue;
            Tanque objetivo = null;
            float mejor = float.MaxValue;
            for (int j = 0; j < jugadores.Length; j++)
            {
                Tanque t = jugadores[j];
                if (!t.vivo) continue;
                float d = Vector3.Distance(e.objeto.transform.position, t.objeto.transform.position);
                if (d < mejor) { mejor = d; objetivo = t; }
            }
            if (objetivo == null) continue;
            Vector3 hacia = objetivo.objeto.transform.position - e.objeto.transform.position;
            hacia.y = 0;
            if (hacia.sqrMagnitude > 0.01f)
            {
                Quaternion rotacion = Quaternion.LookRotation(hacia.normalized);
                e.cuerpo.MoveRotation(Quaternion.RotateTowards(e.cuerpo.rotation, rotacion, 90f * dt));
                if (mejor > 5f) e.cuerpo.MovePosition(e.cuerpo.position + e.objeto.transform.forward * (3.5f * dt));
            }
            if (mejor < 16f && Time.time >= e.siguienteAtaque)
            {
                e.siguienteAtaque = Time.time + 3f + i * 0.3f;
                Disparar(e.objeto.transform.position + e.objeto.transform.forward * 1.6f + Vector3.up * 0.5f, e.objeto.transform.forward, -1);
            }
        }
    }

    void ResolverRonda()
    {
        esperandoRonda = true;
        siguienteRonda = Time.time + 2f;
        if (!jugadores[0].vivo) jugadores[0].derrotas++;
        if (!jugadores[1].vivo) jugadores[1].derrotas++;
        if (jugadores[0].vivo && !jugadores[1].vivo) jugadores[0].victorias++;
        if (jugadores[1].vivo && !jugadores[0].vivo) jugadores[1].victorias++;
        bool p1Fuera = jugadores[0].derrotas >= derrotasMaximas;
        bool p2Fuera = jugadores[1].derrotas >= derrotasMaximas;
        if (p1Fuera || p2Fuera)
        {
            if (p1Fuera && p2Fuera) Finalizar("EMPATE: AMBOS PERDIERON 5 VECES");
            else Finalizar(p1Fuera ? "GANADOR: JUGADOR 2 (ROJO)" : "GANADOR: JUGADOR 1 (AZUL)");
        }
    }

    // Busca un punto aleatorio libre de paredes, coberturas, bots y otros tanques.
    Vector3 PosicionReaparicion()
    {
        for (int intento = 0; intento < 120; intento++)
        {
            Vector3 punto = new Vector3(
                Random.Range(-20f, 20f),
                0.85f,
                Random.Range(-20f, 20f)
            );

            // No reaparecer junto a un bot.
            bool cercaDeEnemigo = false;
            foreach (Enemigo e in enemigos)
            {
                if (e.objeto != null &&
                    Vector3.Distance(punto, e.objeto.transform.position) < 9f)
                {
                    cercaDeEnemigo = true;
                    break;
                }
            }
            if (cercaDeEnemigo) continue;

            // Evita coberturas, paredes y tanques ya colocados.
            Collider[] cercanos = Physics.OverlapBox(
                punto, new Vector3(1.7f, 0.6f, 2.2f)
            );
            bool ocupado = false;
            foreach (Collider c in cercanos)
            {
                if (!c.enabled || c.isTrigger) continue;
                // El suelo no bloquea una reaparicion.
                if (c.gameObject.name == "Terreno") continue;
                ocupado = true;
                break;
            }
            if (!ocupado) return punto;
        }

        // Reserva si el mapa esta demasiado ocupado.
        return new Vector3(Random.Range(-17f, 17f), 0.85f,
                           Random.Range(-17f, 17f));
    }

    void ReiniciarRonda()
    {
        esperandoRonda = false;
        ronda++;
        for (int i = 0; i < balas.Count; i++) if (balas[i].objeto != null) Destroy(balas[i].objeto);
        balas.Clear();
        // Retirar ambos tanques antes de buscar lugares libres.
        for (int i = 0; i < jugadores.Length; i++)
            jugadores[i].objeto.SetActive(false);

        for (int i = 0; i < jugadores.Length; i++)
        {
            Tanque t = jugadores[i];
            Vector3 nuevaPosicion = PosicionReaparicion();
            t.objeto.transform.position = nuevaPosicion;
            t.objeto.transform.rotation = Quaternion.Euler(
                0f, Random.Range(0f, 360f), 0f
            );
            t.cuerpo.position = nuevaPosicion;
            t.cuerpo.rotation = t.objeto.transform.rotation;
            t.cuerpo.linearVelocity = Vector3.zero;
            t.cuerpo.angularVelocity = Vector3.zero;
            t.salud = 100;
            t.vivo = true;
            t.siguienteDisparo = Time.time + 0.5f;
            t.objeto.SetActive(true);
        }
    }

    void Finalizar(string mensaje)
    {
        estado = Estado.Final;
        resultado = mensaje;
        musica.Stop();
    }

    void PrepararGUI()
    {
        if (interfazLista) return;
        fondoGUI = new Texture2D(1, 1);
        fondoGUI.SetPixel(0, 0, new Color(0.05f, 0.09f, 0.15f, 0.82f));
        fondoGUI.Apply();
        titulo = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titulo.normal.textColor = Color.white;
        texto = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
        texto.normal.textColor = Color.white;
        pequeno = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperRight };
        pequeno.normal.textColor = Color.white;
        centro = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        centro.normal.textColor = Color.white;
        boton = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
        interfazLista = true;
    }

    Texture2D texturaAzul, texturaRoja, texturaOscura, texturaVerde;
    Texture2D ColorGUI(ref Texture2D textura, Color color)
    {
        if (textura == null)
        {
            textura = new Texture2D(1, 1);
            textura.SetPixel(0, 0, color);
            textura.Apply();
        }
        return textura;
    }

    void BarraSalud(Rect area, float proporcion, Texture2D color)
    {
        GUI.DrawTexture(area, ColorGUI(ref texturaOscura, new Color(0.04f, 0.08f, 0.12f, 0.9f)));
        float ancho = Mathf.Max(0f, (area.width - 4f) * Mathf.Clamp01(proporcion));
        if (ancho > 0) GUI.DrawTexture(new Rect(area.x + 2f, area.y + 2f, ancho, area.height - 4f), color);
    }

    void OnGUI()
    {
        PrepararGUI();
        // Escala la interfaz con la resolucion, tanto en Editor como en WebGL.
        float escala = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        Matrix4x4 anterior = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(escala, escala, 1));
        float w = Screen.width / escala;
        float h = Screen.height / escala;
        GUI.Label(new Rect(w - 360, 18, 335, 26),
            "Jeffrey Fernandez | 1-21-1476", pequeno);
        if (estado == Estado.Menu)
        {
            GUI.DrawTexture(new Rect(w / 2 - 340, h / 2 - 205, 680, 410), fondoGUI);
            GUI.Label(new Rect(w / 2 - 320, h / 2 - 180, 640, 65), "BATALLA DE TANQUES", titulo);
            GUI.Label(new Rect(w / 2 - 310, h / 2 - 100, 620, 120), "JUGADOR 1: WASD + ESPACIO\nJUGADOR 2: FLECHAS + ENTER\n5 DERROTAS = ELIMINADO | LIMITE: 4 MINUTOS", centro);
            if (GUI.Button(new Rect(w / 2 - 160, h / 2 + 85, 320, 65), "INICIAR PARTIDA", boton)) Iniciar();
        }
        else if (estado == Estado.Jugando)
        {
            Texture2D azul = ColorGUI(ref texturaAzul, new Color(0.11f, 0.63f, 0.98f));
            Texture2D rojo = ColorGUI(ref texturaRoja, new Color(0.98f, 0.25f, 0.27f));
            GUI.DrawTexture(new Rect(16, 16, 340, 116), fondoGUI);
            GUI.DrawTexture(new Rect(w - 356, 55, 340, 116), fondoGUI);
            GUI.DrawTexture(new Rect(w / 2 - 113, 16, 226, 74), fondoGUI);
            GUI.Label(new Rect(30, 21, 315, 28), "JUGADOR 1  |  AZUL", texto);
            GUI.Label(new Rect(w - 342, 60, 315, 28), "JUGADOR 2  |  ROJO", texto);
            GUI.Label(new Rect(30, 50, 310, 24),
                jugadores[0].salud + " HP    DERROTAS " + jugadores[0].derrotas + "/" + derrotasMaximas, texto);
            GUI.Label(new Rect(w - 342, 89, 310, 24),
                jugadores[1].salud + " HP    DERROTAS " + jugadores[1].derrotas + "/" + derrotasMaximas, texto);
            BarraSalud(new Rect(30, 86, 310, 23), jugadores[0].salud / 100f, azul);
            BarraSalud(new Rect(w - 342, 125, 310, 23), jugadores[1].salud / 100f, rojo);
            int segundos = Mathf.CeilToInt(restante);
            GUI.Label(new Rect(w / 2 - 110, 18, 220, 37),
                string.Format("{0:00}:{1:00}", segundos / 60, segundos % 60), titulo);
            GUI.Label(new Rect(w / 2 - 110, 57, 220, 23), "RONDA " + ronda, centro);
            if (esperandoRonda)
            {
                GUI.DrawTexture(new Rect(w / 2 - 255, h / 2 - 40, 510, 80), fondoGUI);
                GUI.Label(new Rect(w / 2 - 250, h / 2 - 34, 500, 68),
                    "PREPARANDO SIGUIENTE RONDA...", centro);
            }
        }
        else if (estado == Estado.Final)
        {
            GUI.DrawTexture(new Rect(w / 2 - 360, h / 2 - 225, 720, 450), fondoGUI);
            GUI.Label(new Rect(w / 2 - 340, h / 2 - 195, 680, 90), "FIN DE LA PARTIDA", titulo);
            GUI.Label(new Rect(w / 2 - 330, h / 2 - 105, 660, 75), resultado, centro);
            GUI.Label(new Rect(w / 2 - 330, h / 2 - 30, 660, 105),
                "Puntuacion azul: " + jugadores[0].victorias + " victorias\n" +
                "Puntuacion rojo: " + jugadores[1].victorias + " victorias\n" +
                "Tiempo utilizado: " + Mathf.FloorToInt(transcurrido / 60f).ToString("00") + ":" + Mathf.FloorToInt(transcurrido % 60f).ToString("00"), centro);
            if (GUI.Button(new Rect(w / 2 - 340, h / 2 + 120, 320, 65), "VOLVER A JUGAR", boton))
                Iniciar();
            if (GUI.Button(new Rect(w / 2 + 20, h / 2 + 120, 320, 65), "MENU PRINCIPAL", boton))
                VolverAlMenu();
        }
        GUI.matrix = anterior;
    }
}
