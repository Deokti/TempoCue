using TempoCue.Tools.ReplayInspect;

// Служебный инструмент разбора сохранённых кадров миникарты. Он не входит в
// поставку приложения и существует, чтобы решения по распознаванию принимались
// по измерениям корпуса, а не по одному наблюдению на экране.
if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

try
{
    return args[0] switch
    {
        "--diag" when args.Length >= 2 =>
            await FrameInspector.PrintRegionDiagnosticsAsync(args.Skip(1)),
        "--probe" when args.Length >= 4 =>
            await FrameInspector.ProbePointsAsync(args[1], args.Skip(2)),
        "--solo" when args.Length >= 2 =>
            await FrameInspector.PrintSoloRecognitionAsync(args.Skip(1)),
        "--stickiness" when args.Length == 2 =>
            await CorpusStatistics.PrintStickinessAsync(args[1]),
        "--calibrate" when args.Length == 2 =>
            await CorpusStatistics.PrintReferenceBaselinesAsync(args[1]),
        "--margin" when args.Length == 2 =>
            await CorpusStatistics.PrintNegativeMarginsAsync(args[1]),
        "--scales" when args.Length == 2 =>
            await ScaleDistribution.RunAsync(args[1]),
        "--ceiling" when args.Length == 2 =>
            await RecoverableMissCeiling.RunAsync(args[1]),
        "--soak" when args.Length is 2 or 3 or 4 =>
            await RecognitionSoak.RunAsync(
                args[1],
                args.Length > 2 ? int.Parse(args[2]) : 10,
                args.Length > 3 ? int.Parse(args[3]) : 8),
        "--timing" when args.Length is 2 or 3 =>
            await RecognitionTiming.RunAsync(args[1], args.Length == 3 ? args[2] : null),
        "--compare" when args.Length == 2 =>
            await DeeperLeagueComparison.RunAsync(args[1]),
        "--model" when args.Length >= 2 =>
            await DeeperLeagueComparison.RunOnFramesAsync(args.Skip(1)),
        "--icons" when args.Length == 3 =>
            await ChampionIconComparison.RunAsync(args[1], args[2]),
        "--icon-recognition" when args.Length == 3 =>
            await ChampionIconComparison.RunRecognitionAsync(args[1], args[2]),
        "--model-raw" when args.Length >= 2 =>
            await DeeperLeagueComparison.RunOnFramesAsync(
                args.Skip(1),
                minimumConfidence: 0.10,
                rejectStaticObjects: false),
        _ when args[0].StartsWith("--", StringComparison.Ordinal) =>
            Fail($"Неизвестный или неполный режим: {args[0]}"),
        _ => await FrameInspector.PrintRecognitionAsync(args)
    };
}
catch (FileNotFoundException exception)
{
    return Fail(exception.Message);
}
catch (InvalidDataException exception)
{
    return Fail($"Файл повреждён или имеет неподдерживаемый формат: {exception.Message}");
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    Console.Error.WriteLine();
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.Error.WriteLine("""
        Разбор сохранённых кадров миникарты.

          ReplayInspect <кадр...>
              что распозналось в кадре и с какой уверенностью

          ReplayInspect --diag <кадр...>
              сырые баллы всех эталонов по каждой области и измерение кольца значка

          ReplayInspect --probe <кадр> <x> <y> [<x> <y>...]
              оценка области в заданной точке, даже если конвейер её не предложил

          ReplayInspect --solo <кадр...>
              прогон с каждым эталоном по отдельности, без конкуренции состава

          ReplayInspect --stickiness <кадр>
              липкость эталонов друг о друга, измеренная офлайн из самих портретов

          ReplayInspect --calibrate <каталог корпуса>
              базовый уровень каждого эталона на своих и чужих областях

          ReplayInspect --margin <каталог корпуса>
              запас лучшего балла над отрицательными эталонами

          ReplayInspect --ceiling <каталог корпуса>
              потолок детектора: сколько пропусков осталось без области сравнения
              и сколько с областью, но без подтверждения личности

          ReplayInspect --soak <кадр> [минут] [кадр/с]
              долгий прогон одного кадра: растёт ли стоимость или память со временем

          ReplayInspect --timing <каталог корпуса> [<каталог модели>]
              стоимость одного кадра в установившемся режиме и частота, которую
              эта стоимость позволяет; каталог модели необязателен

          ReplayInspect --compare <каталог корпуса>
              сравнение основного распознавателя и модели DeeperLeague на одной
              разметке; требует models/deeperleague с best.onnx и champMap.json

        Координаты точек задаются в канонической системе кадра 320 x 320.
        """);
}
