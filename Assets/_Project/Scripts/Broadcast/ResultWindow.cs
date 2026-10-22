using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Radio.Broadcast
{
    /// <summary>
    /// Окно результата заявки (GDD 02.22): сначала вырастает фигура кассет, затем фигура
    /// заявки, появляется процент, потом из центра вылетает точка и, отскакивая от стенок
    /// заявки, останавливается — внутри фигуры кассет успех, снаружи провал.
    /// </summary>
    /// <remarks>
    /// Исход уже решён в <see cref="BroadcastGame.Evaluate"/>; окно его только показывает.
    /// Первый клик по кнопке во время анимации доматывает её до конца, второй закрывает.
    /// </remarks>
    internal sealed class ResultWindow
    {
        private const float ShapeGrow = 0.5f;

        // После того как выросли обе фигуры, поле плавно приближается к ним: на шкале 0–10
        // фигуры ранних заявок занимают треть поля, и полёт точки было бы не разглядеть.
        private const float ZoomAt = 1.0f;
        private const float ZoomTime = 0.7f;
        private const float PercentAt = 1.7f;
        private const float DotAt = 2.0f;
        private const float TextDelay = 0.35f;

        private static readonly Color DotFlying = Color.white;
        private static readonly Color DotSuccess = new Color(0.35f, 0.85f, 0.35f, 1f);
        private static readonly Color DotFail = Color.black;

        private static readonly Color RequestFill = new Color(0.75f, 0.75f, 0.72f, 0.16f);
        private static readonly Color RequestOutline = new Color(0.85f, 0.85f, 0.8f, 0.9f);
        private static readonly Color NormalFill = new Color(0.95f, 0.66f, 0.18f, 0.5f);
        private static readonly Color StrangeFill = new Color(0.9f, 0.22f, 0.2f, 0.55f);

        private readonly GameObject _root;
        private readonly RadarGraphic _radar;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _cassettes;
        private readonly TextMeshProUGUI _chance;
        private readonly TextMeshProUGUI _outcome;
        private readonly TextMeshProUGUI _response;
        private readonly TextMeshProUGUI _legend;
        private readonly Button _close;
        private readonly TextMeshProUGUI[] _axisLabels = new TextMeshProUGUI[VibeRadar.Axes];
        private readonly List<TextMeshProUGUI> _ticks = new List<TextMeshProUGUI>();

        private readonly RadarGraphic.Shape _strange = new RadarGraphic.Shape { Fill = StrangeFill, Outline = BroadcastUi.Red };
        private readonly RadarGraphic.Shape _normal = new RadarGraphic.Shape { Fill = NormalFill, Outline = BroadcastUi.Amber };
        private readonly RadarGraphic.Shape _request = new RadarGraphic.Shape { Fill = RequestFill, Outline = RequestOutline };

        private TapeItem _item;
        private CheckResult _result;
        private float _time;
        private float _end;
        private float _stopAt;
        private float _scale;
        private float _zoom;
        private Action _onClosed;

        public bool IsOpen => _root.activeSelf;

        public ResultWindow(RectTransform parent)
        {
            var panel = BroadcastUi.Box(parent, "ResultWindow", BroadcastUi.Panel);
            BroadcastUi.Border(panel, BroadcastUi.Amber);
            _root = panel.gameObject;
            var rect = panel.rectTransform;
            BroadcastUi.Stretch(rect);

            var radarRect = BroadcastUi.Place(BroadcastUi.Rect(rect, "Radar"), 70f, 120f, 600f, 600f);
            radarRect.gameObject.AddComponent<CanvasRenderer>();
            _radar = radarRect.gameObject.AddComponent<RadarGraphic>();
            _radar.raycastTarget = false;
            _radar.Shapes.Add(_strange);
            _radar.Shapes.Add(_normal);
            _radar.Shapes.Add(_request);

            for (var i = 0; i < VibeRadar.Axes; i++)
            {
                var label = BroadcastUi.Label(radarRect, "Axis" + i, VibeRadar.Names[i], 24f,
                    BroadcastUi.Muted, TextAlignmentOptions.Center);
                label.rectTransform.sizeDelta = new Vector2(180f, 34f);
                _axisLabels[i] = label;
            }

            // Деления шкалы вдоль верхней оси: 2, 4, 6, 8, 10.
            for (var i = 1; i <= 5; i++)
            {
                var tick = BroadcastUi.Label(radarRect, "Tick" + i, string.Empty, 18f,
                    new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Left);
                tick.rectTransform.sizeDelta = new Vector2(60f, 24f);
                _ticks.Add(tick);
            }

            _title = BroadcastUi.Label(rect, "Title", string.Empty, 38f, BroadcastUi.Text);
            BroadcastUi.Place(_title.rectTransform, 40f, 30f, 1360f, 60f);

            _legend = BroadcastUi.Label(rect, "Legend", string.Empty, 22f, BroadcastUi.Muted);
            BroadcastUi.Place(_legend.rectTransform, 70f, 760f, 640f, 80f);

            const float column = 760f;
            _cassettes = BroadcastUi.Label(rect, "Cassettes", string.Empty, 28f, BroadcastUi.Text);
            BroadcastUi.Place(_cassettes.rectTransform, column, 130f, 620f, 120f);

            _chance = BroadcastUi.Label(rect, "Chance", string.Empty, 64f, BroadcastUi.Amber);
            BroadcastUi.Place(_chance.rectTransform, column, 260f, 620f, 90f);

            _outcome = BroadcastUi.Label(rect, "Outcome", string.Empty, 46f, BroadcastUi.Text);
            BroadcastUi.Place(_outcome.rectTransform, column, 360f, 620f, 70f);

            _response = BroadcastUi.Label(rect, "Response", string.Empty, 28f, BroadcastUi.Text);
            BroadcastUi.Place(_response.rectTransform, column, 440f, 620f, 300f);

            _close = BroadcastUi.Button(rect, "Close", "Пропустить", OnClose);
            BroadcastUi.Place((RectTransform)_close.transform, column, 780f, 300f, 70f);

            _root.SetActive(false);
        }

        /// <param name="scale">Сколько вайба от центра до края: шкала одна для всех заявок.</param>
        public void Open(TapeItem item, CheckResult result, float scale, Action onClosed)
        {
            _item = item;
            _result = result;
            _onClosed = onClosed;
            _time = 0f;
            _stopAt = DotAt + (result.Path.Count - 1) / 60f;
            _end = _stopAt + TextDelay;

            var request = item.Source.Request;
            var faction = string.IsNullOrWhiteSpace(request.Faction) || request.Faction == "Нет" ? string.Empty : $"  <color=#9E9E94>· {request.Faction}</color>";
            _title.text = $"{request.Listener}{faction}";

            var names = new List<string>();
            foreach (var slot in item.Cassettes)
            {
                var color = slot.Data.Strange ? "#E5403A" : "#F2A82E";
                names.Add($"<color={color}>{slot.Data.Id}</color> {slot.Data.Title}");
            }

            _cassettes.text = string.Join("\n", names);
            _legend.text = "<color=#F2A82E>■</color> фигура кассет   " +
                           (result.HasStrange ? "<color=#E5403A>■</color> вклад странных   " : string.Empty) +
                           "<color=#D9D9CC>□</color> фигура заявки";

            // Шкала постоянная (0–10): заявки разного уровня сравнимы на глаз. На время
            // проверки поле приближается к фигурам, деления при этом остаются в своих значениях.
            _scale = Mathf.Max(1f, scale);
            var fit = 0f;

            foreach (var radii in new[] { result.Request, result.All })
            {
                foreach (var r in radii)
                {
                    fit = Mathf.Max(fit, r);
                }
            }

            _zoom = Mathf.Clamp(fit * 1.25f, 2.5f, _scale);
            _radar.GridMax = _scale;
            _radar.Reach = _scale;
            PlaceAxisLabels();

            _strange.Radii = result.HasStrange ? result.All : null;
            _normal.Radii = result.Normal;
            _request.Radii = result.Request;

            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            Apply();
        }

        public void Tick(float dt)
        {
            if (!IsOpen || _time >= _end)
            {
                return;
            }

            _time += dt;
            Apply();
        }

        private void Apply()
        {
            var t = _time;
            var cassetteScale = Mathf.SmoothStep(0f, 1f, t / ShapeGrow);
            var requestScale = Mathf.SmoothStep(0f, 1f, (t - ShapeGrow) / ShapeGrow);
            _strange.Scale = cassetteScale;
            _normal.Scale = cassetteScale;
            _request.Scale = requestScale;

            _radar.Reach = Mathf.Lerp(_scale, _zoom, Mathf.SmoothStep(0f, 1f, (t - ZoomAt) / ZoomTime));
            PlaceAxisLabels();

            _chance.text = t >= PercentAt ? $"Шанс {Mathf.RoundToInt(_result.Chance * 100f)} %" : string.Empty;

            var path = _result.Path;
            var index = Mathf.FloorToInt((t - DotAt) * 60f);
            _radar.ShowDot = t >= DotAt;
            _radar.Dot = path[Mathf.Clamp(index, 0, path.Count - 1)];

            // Остановилась — точка окрашивается исходом и коротко вспухает.
            var stopped = t >= _stopAt;
            _radar.DotColor = !stopped ? DotFlying
                : _result.Outcome == CheckOutcome.Success ? DotSuccess
                : _result.Outcome == CheckOutcome.StrangeSuccess ? BroadcastUi.Red
                : DotFail;
            _radar.DotRadius = stopped ? 9f * (1f + 0.6f * Mathf.Exp(-(t - _stopAt) * 6f)) : 9f;

            var done = t >= _end;

            if (done)
            {
                var request = _item.Source.Request;
                _outcome.text = _result.Outcome switch
                {
                    CheckOutcome.Success => "<color=#73CC66>Подошло</color>",
                    CheckOutcome.StrangeSuccess => "<color=#E5403A>Подошло… · Хтонь +1</color>",
                    _ => "<color=#E5403A>Не подошло</color>",
                };

                var line = _result.Success ? request.SuccessText : request.FailText;
                var effect = _result.Success ? request.SuccessResult : request.FailResult;
                _response.text = $"«{line}»\n\n<color=#9E9E94><size=80%>{effect}</size></color>";
            }
            else
            {
                _outcome.text = string.Empty;
                _response.text = string.Empty;
            }

            BroadcastUi.SetButtonText(_close, done ? "Закрыть" : "Пропустить");
            _radar.Refresh();
        }

        private void PlaceAxisLabels()
        {
            var size = 600f;
            var pixels = size * 0.5f / _radar.Reach;

            for (var i = 0; i < VibeRadar.Axes; i++)
            {
                var label = _axisLabels[i].rectTransform;
                label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
                label.pivot = new Vector2(0.5f, 0.5f);
                label.anchoredPosition = VibeRadar.Axis(i) * (_radar.Reach * pixels + 26f);
            }

            // Деления стоят в своих значениях; ушедшие за край при приближении прячем.
            for (var i = 0; i < _ticks.Count; i++)
            {
                var value = _radar.GridStep * (i + 1);
                var tick = _ticks[i].rectTransform;
                tick.anchorMin = tick.anchorMax = new Vector2(0.5f, 0.5f);
                tick.pivot = new Vector2(0f, 0.5f);
                tick.anchoredPosition = VibeRadar.Axis(0) * (value * pixels) + new Vector2(8f, 0f);
                _ticks[i].text = Mathf.RoundToInt(value).ToString();
                _ticks[i].gameObject.SetActive(value <= _radar.Reach + 1e-3f);
            }
        }

        private void OnClose()
        {
            if (_time < _end)
            {
                _time = _end;
                Apply();
                return;
            }

            _root.SetActive(false);
            var callback = _onClosed;
            _onClosed = null;
            callback?.Invoke();
        }
    }
}
