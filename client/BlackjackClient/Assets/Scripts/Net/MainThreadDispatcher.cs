using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blackjack.Client.Net
{
    /// <summary>
    /// Traslada trabajo al hilo principal de Unity.
    ///
    /// SignalR entrega sus callbacks en hilos del pool, y casi toda la API de
    /// Unity (transform, GameObject, UI...) lanza excepción si se toca fuera
    /// del hilo principal. Sin este puente, el cliente parecería funcionar
    /// hasta el primer intento de mover una carta.
    /// </summary>
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static readonly Queue<Action> _pending = new Queue<Action>();

        /// <summary>
        /// Crea el despachador si aún no existe. Debe llamarse desde el hilo
        /// principal, típicamente antes de abrir la conexión.
        /// </summary>
        public static void EnsureExists()
        {
            if (_instance != null) return;

            var go = new GameObject("[MainThreadDispatcher]");
            _instance = go.AddComponent<MainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }

        /// <summary>
        /// Encola una acción para ejecutarla en el próximo Update. Es seguro
        /// llamarlo desde cualquier hilo.
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null) return;

            lock (_pending)
            {
                _pending.Enqueue(action);
            }
        }

        private void Update()
        {
            // Se vacía la cola a un buffer local antes de ejecutar: una acción
            // puede encolar otra, y hacerlo dentro del lock se bloquearía sola.
            Action[] batch;

            lock (_pending)
            {
                if (_pending.Count == 0) return;

                batch = new Action[_pending.Count];
                _pending.CopyTo(batch, 0);
                _pending.Clear();
            }

            foreach (Action action in batch)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    // Una acción que falle no debe impedir que se ejecuten las
                    // siguientes ni dejar la cola atascada.
                    Debug.LogException(ex);
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
