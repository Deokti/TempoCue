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
        "--sequence" when args.Length == 2 =>
            await SequenceListing.RunAsync(args[1]),
        "--stability" when args.Length == 2 =>
            await SequenceStability.RunAsync(args[1]),
        "--visibility" when args.Length == 2 =>
            await VisibilityRules.RunAsync(args[1]),
        "--scale-sweep" when args.Length == 2 =>
            await ScaleSweep.RunAsync(args[1]),
        "--scales" when args.Length == 2 =>
            await ScaleDistribution.RunAsync(args[1]),
        "--ceiling" when args.Length == 2 =>
            await RecoverableMissCeiling.RunAsync(args[1]),
        "--soak" when args.Length is 2 or 3 or 4 =>
            await RecognitionSoak.RunAsync(
                args[1],
                args.Length > 2 ? int.Parse(args[2]) : 10,
                args.Length > 3 ? int.Parse(args[3]) : 8),
        "--timing" when args.Length == 2 =>
            await RecognitionTiming.RunAsync(args[1]),
        "--own-model" when args.Length == 2 =>
            await OwnModelComparison.RunAsync(args[1]),
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

          ReplayInspect --stability <запись.tempocue-sequence>
              устойчивость между кадрами: на какой доле кадров участник
              подтверждён, пока он виден всю запись

          ReplayInspect --ceiling <каталог корпуса>
              потолок детектора: сколько пропусков осталось без области сравнения
              и сколько с областью, но без подтверждения личности

          ReplayInspect --soak <кадр> [минут] [кадр/с]
              долгий прогон одного кадра: растёт ли стоимость или память со временем

          ReplayInspect --timing <каталог корпуса>
              стоимость одного кадра штатного конвейера в установившемся режиме
              и частота, которую эта стоимость позволяет; сравнение со своей
              моделью даёт --own-model

          ReplayInspect --own-model <каталог корпуса>
              сравнение основного распознавателя и своей модели на одной
              разметке; требует models/own-model с model.json, detector.onnx
              и identity.onnx — их отдаёт tools/export_onnx.py модельного
              репозитория

        Координаты точек задаются в канонической системе кадра 320 x 320.
        """);
}
