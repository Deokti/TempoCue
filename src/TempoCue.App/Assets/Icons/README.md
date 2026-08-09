# TempoCue — иконки

41 иконок. Сетка 20 x 20, обводка 1.5, скругления round, только геометрические примитивы
(окружность, прямоугольник, линия, ломаная) — всё воспроизводится в WPF без растровых ассетов.

## Что в архиве

- `svg/` — исходники SVG. `stroke="currentColor"`, поэтому цвет наследуется от родителя.
- `TempoCue.Icons.xaml` — ResourceDictionary для WPF: кисти палитры, стиль `TempoIcon` и геометрии.

## Как подключать в WPF

WPF не умеет SVG напрямую, поэтому иконки уже переведены в `StreamGeometry` — это самый
дешёвый и самый резкий вариант: никакой конвертации при сборке, масштабирование без потерь,
цвет меняется через `Stroke`.

```xml
<Path Style="{StaticResource TempoIcon}" Data="{StaticResource Icon.NavHome}"/>
```

Кнопка с иконкой и подписью:

```xml
<Button Height="40" Padding="12,0">
    <StackPanel Orientation="Horizontal">
        <Path Style="{StaticResource TempoIcon}" Data="{StaticResource Icon.ActPlus}"
              Stroke="{StaticResource Accent.Primary}" Margin="0,0,12,0"/>
        <TextBlock Text="Создать набор советов" VerticalAlignment="Center"/>
    </StackPanel>
</Button>
```

Смена цвета по состоянию — через триггер на `Stroke`, саму геометрию менять не нужно.

## Альтернативы, если набора не хватит

- **Segoe Fluent Icons** — встроен в Windows 11, ~1600 глифов, ставится как `FontFamily`
  и не требует ассетов. На Windows 10 его нет: там **Segoe MDL2 Assets**. Годится для
  системных действий (закрыть, свернуть, обновить), но глифы залиты и не имеют тонкой обводки —
  рядом с этим набором будут выглядеть плотнее.
- **SharpVectors** или **SVG to XAML** — если понадобится втянуть чужие SVG целиком.
  Для набора такого размера это лишняя зависимость.
- **Fluent System Icons** (открытый набор Microsoft) — есть готовые XAML-геометрии,
  но стиль заливной, а не штриховой.

Практический вывод: этот набор для смыслового ядра интерфейса, Segoe Fluent Icons —
для системных кнопок окна, если решите не рисовать их сами.

## Состав

| Файл | Назначение | Ключ в XAML |
|---|---|---|
| `nav-home.svg` | Главная | `Icon.NavHome` |
| `nav-match.svg` | Текущий матч | `Icon.NavMatch` |
| `nav-coaching.svg` | Коучинг | `Icon.NavCoaching` |
| `nav-history.svg` | История матчей | `Icon.NavHistory` |
| `nav-settings.svg` | Настройки | `Icon.NavSettings` |
| `nav-packs.svg` | Наборы советов | `Icon.NavPacks` |
| `state-ok.svg` | Всё в порядке | `Icon.StateOk` |
| `state-caution.svg` | Осторожно | `Icon.StateCaution` |
| `state-danger.svg` | Опасность | `Icon.StateDanger` |
| `state-info.svg` | Информация | `Icon.StateInfo` |
| `state-pause.svg` | Пауза | `Icon.StatePause` |
| `state-waiting.svg` | Ожидание | `Icon.StateWaiting` |
| `state-offline.svg` | Нет соединения | `Icon.StateOffline` |
| `state-refresh.svg` | Повторить | `Icon.StateRefresh` |
| `mark-fact.svg` | Факт | `Icon.MarkFact` |
| `mark-assumption.svg` | Предположение | `Icon.MarkAssumption` |
| `mark-coach.svg` | Тренерский совет | `Icon.MarkCoach` |
| `mark-experiment.svg` | Эксперимент | `Icon.MarkExperiment` |
| `act-plus.svg` | Создать | `Icon.ActPlus` |
| `act-import.svg` | Импорт | `Icon.ActImport` |
| `act-export.svg` | Экспорт | `Icon.ActExport` |
| `act-search.svg` | Поиск | `Icon.ActSearch` |
| `act-copy.svg` | Дублировать | `Icon.ActCopy` |
| `act-delete.svg` | Удалить | `Icon.ActDelete` |
| `act-close.svg` | Закрыть | `Icon.ActClose` |
| `act-chevron.svg` | Раскрыть | `Icon.ActChevron` |
| `act-back.svg` | Назад | `Icon.ActBack` |
| `act-drag.svg` | Перетащить | `Icon.ActDrag` |
| `act-more.svg` | Ещё | `Icon.ActMore` |
| `act-collapse.svg` | Свернуть меню | `Icon.ActCollapse` |
| `act-check.svg` | Я увидел совет | `Icon.ActCheck` |
| `ov-overlay.svg` | Игровой оверлей | `Icon.OvOverlay` |
| `ov-locked.svg` | Положение закреплено | `Icon.OvLocked` |
| `ov-unlocked.svg` | Положение свободно | `Icon.OvUnlocked` |
| `ov-hidden.svg` | Оверлей скрыт | `Icon.OvHidden` |
| `ov-visible.svg` | Оверлей виден | `Icon.OvVisible` |
| `map-minimap.svg` | Миникарта | `Icon.MapMinimap` |
| `map-lane.svg` | Линия | `Icon.MapLane` |
| `map-objective.svg` | Нейтральный объект | `Icon.MapObjective` |
| `map-timer.svg` | Таймер | `Icon.MapTimer` |
| `map-vision.svg` | Обзор | `Icon.MapVision` |
