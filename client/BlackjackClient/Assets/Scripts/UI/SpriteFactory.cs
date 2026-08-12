using System.Collections.Generic;
using UnityEngine;

namespace Blackjack.Client.UI
{
    /// <summary>
    /// Genera las imágenes de la mesa por código.
    ///
    /// Nada de assets externos: el fieltro, las cartas y las fichas se dibujan
    /// en texturas al arrancar. Así el proyecto no arrastra licencias de arte
    /// ni descargas, y cambiar el aspecto es tocar <see cref="TableTheme"/>.
    ///
    /// Todo se cachea: generar texturas es caro y estas no cambian nunca.
    /// </summary>
    public static class SpriteFactory
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Fieltro con grano y viñeta. El grano es lo que evita que se vea
        /// como un plano de color: una mesa real tiene textura de tela.
        /// </summary>
        public static Sprite Felt(int size = 512)
        {
            string key = "felt" + size;
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(size, size);
            float half = size * 0.5f;
            float maxDistance = Mathf.Sqrt(half * half + half * half);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) / maxDistance;

                    // Viñeta: la luz cenital cae hacia los bordes.
                    Color color = Color.Lerp(TableTheme.FeltCenter, TableTheme.FeltEdge,
                        Mathf.SmoothStep(0f, 1f, distance * 1.15f));

                    // Grano fino de tela, apenas perceptible pero suficiente
                    // para romper la planitud del degradado.
                    float grain = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) - 0.5f;
                    color.r += grain * 0.035f;
                    color.g += grain * 0.045f;
                    color.b += grain * 0.035f;

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Store(key, texture);
        }

        /// <summary>
        /// La mesa: una elipse de fieltro con borde acolchado y el arco de
        /// apuestas dibujado encima.
        ///
        /// La forma es lo que más se nota. Un rectángulo verde nunca parece
        /// una mesa de blackjack; el semicírculo con el croupier en el lado
        /// recto es la silueta que identifica el juego de un vistazo.
        ///
        /// El arco va en esta misma textura para que no pueda descuadrarse
        /// respecto al fieltro; los huecos de apuesta se colocan luego sobre
        /// él con la misma ecuación de elipse.
        /// </summary>
        public static Sprite TableSurface(int width, int height, float arcRadius = 0.85f)
        {
            string key = $"table{width}x{height}a{arcRadius}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(width, height);

            float cx = width * 0.5f;
            float cy = height * 0.5f;
            float rx = cx;
            float ry = cy;

            const float railFrom = 0.93f;   // dónde empieza la madera
            const float arcHalfWidth = 0.012f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x - cx) / rx;
                    float ny = (y - cy) / ry;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);

                    if (d > 1f)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    Color color;

                    if (d > railFrom)
                    {
                        // Borde de madera con un bisel: más claro por dentro,
                        // más oscuro hacia fuera.
                        float k = Mathf.InverseLerp(railFrom, 1f, d);
                        color = Color.Lerp(TableTheme.RailInner, TableTheme.RailOuter, k);
                        color.r += Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.05f - 0.025f;
                    }
                    else
                    {
                        // Fieltro: claro en el centro por la luz cenital, más
                        // apagado hacia el borde.
                        float k = Mathf.SmoothStep(0f, 1f, d / railFrom);
                        color = Color.Lerp(TableTheme.FeltCenter, TableTheme.FeltEdge, k * 0.95f);

                        float grain = Mathf.PerlinNoise(x * 0.55f, y * 0.55f) - 0.5f;
                        color.r += grain * 0.030f;
                        color.g += grain * 0.040f;
                        color.b += grain * 0.030f;

                        // Arco de apuestas, en la misma línea donde se sientan
                        // los jugadores.
                        float toArc = Mathf.Abs(d - arcRadius);
                        if (toArc < arcHalfWidth && ny > -0.15f)
                        {
                            float fade = 1f - toArc / arcHalfWidth;
                            color = Color.Lerp(color, TableTheme.SpotLine, fade * 0.85f);
                        }
                    }

                    // Suavizado del contorno exterior.
                    if (d > 0.995f) color.a *= Mathf.InverseLerp(1f, 0.995f, d);

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Store(key, texture);
        }

        /// <summary>Rectángulo de esquinas redondeadas, con borde opcional.</summary>
        public static Sprite RoundedRect(int width, int height, int radius, Color fill,
            Color border = default, int borderWidth = 0)
        {
            string key = $"rr{width}x{height}r{radius}f{fill}b{border}w{borderWidth}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float distance = RoundedRectDistance(x, y, width, height, radius);

                    if (distance > 0.5f)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    Color color = fill;

                    if (borderWidth > 0 && distance > -borderWidth) color = border;

                    // Suavizado del borde para que no salga dentado.
                    if (distance > -0.5f) color.a *= Mathf.InverseLerp(0.5f, -0.5f, distance);

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Store(key, texture, radius);
        }

        /// <summary>Círculo relleno con borde, para las fichas.</summary>
        public static Sprite Circle(int diameter, Color fill, Color border = default, int borderWidth = 0)
        {
            string key = $"c{diameter}f{fill}b{border}w{borderWidth}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(diameter, diameter);
            float radius = diameter * 0.5f;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x - radius + 0.5f;
                    float dy = y - radius + 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;

                    if (distance > 0.5f)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    Color color = borderWidth > 0 && distance > -borderWidth ? border : fill;
                    if (distance > -0.5f) color.a *= Mathf.InverseLerp(0.5f, -0.5f, distance);

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Store(key, texture);
        }

        /// <summary>
        /// Ficha de casino: color de fondo, muescas blancas en el canto, aro
        /// interior y centro claro donde va el valor.
        ///
        /// Las muescas del canto son lo que la hace reconocible. Un círculo de
        /// color liso parece un botón; con ellas parece arcilla prensada.
        /// </summary>
        public static Sprite Chip(int diameter, Color body, int notches = 6)
        {
            string key = $"chip{diameter}{body}{notches}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(diameter, diameter);

            float center = diameter * 0.5f;
            var white = new Color(0.97f, 0.97f, 0.95f);
            Color rimShadow = Color.Lerp(body, Color.black, 0.35f);
            Color faceTint = Color.Lerp(body, white, 0.18f);

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float r = distance / center;

                    if (r > 1f)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    // Ángulo normalizado 0..1 alrededor de la ficha.
                    float angle = (Mathf.Atan2(dy, dx) + Mathf.PI) / (Mathf.PI * 2f);
                    float sector = angle * notches % 1f;
                    bool onNotch = sector < 0.42f;

                    Color color;

                    if (r > 0.955f) color = rimShadow;                       // canto exterior
                    else if (r > 0.74f) color = onNotch ? white : body;      // muescas
                    else if (r > 0.695f) color = rimShadow;                  // filo del aro
                    else if (r > 0.60f) color = white;                       // aro interior
                    else if (r > 0.545f) color = rimShadow;
                    else if (r > 0.50f) color = faceTint;
                    else color = white;                                      // centro del valor

                    if (r > 0.99f) color.a *= Mathf.InverseLerp(1f, 0.99f, r);

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Store(key, texture);
        }

        /// <summary>
        /// Círculo de trazo discontinuo: el hueco de apuesta del tapete, igual
        /// que el arco amarillo de las mesas de referencia.
        /// </summary>
        public static Sprite DashedCircle(int diameter, Color line, int thickness = 3, int dashes = 28)
        {
            string key = $"dc{diameter}{line}{thickness}{dashes}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(diameter, diameter);
            float radius = diameter * 0.5f;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x - radius + 0.5f;
                    float dy = y - radius + 0.5f;
                    float distance = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - (radius - thickness));

                    if (distance > thickness * 0.5f)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    // El ángulo decide si este punto cae en trazo o en hueco.
                    float angle = Mathf.Atan2(dy, dx) + Mathf.PI;
                    float segment = angle / (Mathf.PI * 2f) * dashes;
                    bool onDash = segment % 1f < 0.55f;

                    if (!onDash)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    Color color = line;
                    color.a *= Mathf.InverseLerp(thickness * 0.5f, thickness * 0.5f - 1f, distance);
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Store(key, texture);
        }

        /// <summary>Dorso de carta con rombo, para las cartas tapadas y el zapato.</summary>
        public static Sprite CardBack(int width, int height)
        {
            string key = $"back{width}x{height}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(width, height);
            const int radius = 8;
            const int margin = 6;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float distance = RoundedRectDistance(x, y, width, height, radius);

                    if (distance > 0.5f)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    Color color = TableTheme.CardFace;

                    if (x > margin && x < width - margin && y > margin && y < height - margin)
                    {
                        // Rejilla de rombos, el patrón clásico del reverso.
                        float u = (x - margin) / 9f;
                        float v = (y - margin) / 9f;
                        bool diamond = (Mathf.Abs(u % 2f - 1f) + Mathf.Abs(v % 2f - 1f)) < 1f;
                        color = diamond ? TableTheme.CardBackA : TableTheme.CardBackB;
                    }

                    if (distance > -0.5f) color.a *= Mathf.InverseLerp(0.5f, -0.5f, distance);

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Store(key, texture, radius);
        }

        /// <summary>Degradado vertical suave, para paneles y bandas.</summary>
        public static Sprite VerticalGradient(int height, Color top, Color bottom)
        {
            string key = $"vg{height}{top}{bottom}";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var texture = NewTexture(4, height);

            for (int y = 0; y < height; y++)
            {
                Color color = Color.Lerp(bottom, top, y / (float)(height - 1));
                for (int x = 0; x < 4; x++) texture.SetPixel(x, y, color);
            }

            texture.Apply();
            return Store(key, texture);
        }

        public static Sprite Solid(Color color) => RoundedRect(8, 8, 0, color);

        // ------------------------------------------------------------------

        /// <summary>
        /// Distancia con signo al borde de un rectángulo redondeado. Negativa
        /// dentro, positiva fuera; es lo que permite suavizar el contorno en
        /// vez de dejarlo dentado.
        /// </summary>
        private static float RoundedRectDistance(int x, int y, int width, int height, int radius)
        {
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            float px = Mathf.Abs(x - halfW + 0.5f) - (halfW - radius);
            float py = Mathf.Abs(y - halfH + 0.5f) - (halfH - radius);

            float outside = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f) +
                                       Mathf.Max(py, 0f) * Mathf.Max(py, 0f));

            return outside + Mathf.Min(Mathf.Max(px, py), 0f) - radius + radius;
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            return texture;
        }

        private static Sprite Store(string key, Texture2D texture, int border = 0)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border > 0 ? new Vector4(border, border, border, border) : Vector4.zero);

            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
