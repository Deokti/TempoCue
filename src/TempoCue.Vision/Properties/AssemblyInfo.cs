using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TempoCue.Vision.Tests")]

// Экспериментальный распознаватель переиспользует отрицательные эталоны статичных
// объектов, чтобы проверить гибрид: сторонний детектор как источник кандидатов при
// нашей защите от ложных подтверждений. Связь временная и живёт вместе с экспериментом.
[assembly: InternalsVisibleTo("TempoCue.Vision.DeeperLeague")]
