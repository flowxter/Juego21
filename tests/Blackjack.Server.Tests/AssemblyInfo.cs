using Xunit;

// Los tests de integración van en serie, no en paralelo.
//
// Cada clase levanta su propio servidor en memoria contra la MISMA base de
// datos, y las mesas avanzan por temporizadores reales. En paralelo compiten
// por la base y por el reloj: una espera de 3 segundos se queda corta bajo
// carga y el test falla sin que nada esté roto.
//
// La suite tarda más, pero un test que falla una de cada cinco veces es peor
// que un test lento: deja de creerse y se acaba ignorando.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
