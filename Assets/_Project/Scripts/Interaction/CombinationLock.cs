using System;
using Radio.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Radio.Interaction
{
    /// <summary>
    /// Кодовый замок с колёсами-цифрами. Берётся как обычный «3D просмотр»; пока он в руках,
    /// A и D выбирают колесо, W и S листают цифры. Верный код открывает замок: он исчезает,
    /// а связанная дверь распахивается.
    /// </summary>
    /// <remarks>
    /// Колёса выбираются слева направо так, как их видит игрок, а не по порядку в модели:
    /// порядок экспорта и сторона, которой замок повернут к камере, иначе перепутали бы
    /// «первое» колесо с последним.
    /// Цифра на колесе — поворот на шаг вокруг его оси. Ноль — положение, в котором колесо
    /// лежит в сцене.
    /// </remarks>
    [RequireComponent(typeof(InspectableItem))]
    public sealed class CombinationLock : MonoBehaviour
    {
        [Header("Колёса")]
        [Tooltip("Колёса с цифрами. Порядок неважен: слева направо их расставит сам замок.")]
        [SerializeField] private Transform[] wheels = Array.Empty<Transform>();

        [Tooltip("Ось вращения колеса в его собственных координатах.")]
        [SerializeField] private Vector3 wheelAxis = Vector3.right;

        [Tooltip("Поворот на одну цифру, градусов. Знак задаёт, в какую сторону цифры растут.")]
        [SerializeField] private float stepDegrees = 36f;

        [SerializeField] private float turnSpeed = 540f;

        [Tooltip("Насколько темнее невыбранные колёса: 1 — не темнее, 0 — чёрные.")]
        [SerializeField] private float idleBrightness = 0.55f;

        [Header("Код")]
        [Tooltip("Цифры слева направо, как их видит игрок.")]
        [SerializeField] private string code = "404";

        [Header("Итог")]
        [SerializeField] private DoorOpener door;

        [SerializeField] private string openedText = "Замок открыт";

        [Tooltip("Флаг мира, который ставится при открытии. Пусто — никакого.")]
        [SerializeField] private string openedFlag = "ACTION_LOCK_OPENED";

        [Tooltip("Сколько подержать открытый код перед тем, как замок исчезнет, с.")]
        [SerializeField] private float openDelay = 0.6f;

        [Tooltip("Шрифт надписи. Обязан содержать кириллицу.")]
        [SerializeField] private TMP_FontAsset font;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private InspectableItem _item;
        private Quaternion[] _rest;
        private int[] _digits;
        private float[] _angles;
        private int[] _order;
        private Renderer[] _renderers;
        private Color[] _baseColors;
        private MaterialPropertyBlock _block;
        private int _selected;
        private bool _wasHeld;
        private float _openAt = -1f;

        private void Awake()
        {
            _item = GetComponent<InspectableItem>();
            _rest = new Quaternion[wheels.Length];
            _digits = new int[wheels.Length];
            _angles = new float[wheels.Length];
            _order = new int[wheels.Length];
            _renderers = new Renderer[wheels.Length];
            _baseColors = new Color[wheels.Length];
            _block = new MaterialPropertyBlock();

            for (var i = 0; i < wheels.Length; i++)
            {
                _rest[i] = wheels[i].localRotation;
                _order[i] = i;
                _renderers[i] = wheels[i].GetComponent<Renderer>();
                var material = _renderers[i] != null ? _renderers[i].sharedMaterial : null;
                _baseColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            }
        }

        private void Update()
        {
            AnimateWheels();

            if (_openAt >= 0f)
            {
                if (Time.time >= _openAt)
                {
                    Open();
                }

                return;
            }

            var held = _item.IsHeld;

            if (held && !_wasHeld)
            {
                SortWheelsOnScreen();
                _selected = 0;
            }

            if (held != _wasHeld)
            {
                Highlight(held);
            }

            _wasHeld = held;

            if (!held)
            {
                return;
            }

            ReadInput();
        }

        private void ReadInput()
        {
            var keyboard = Keyboard.current;

            if (keyboard == null || wheels.Length == 0)
            {
                return;
            }

            // Позиционные коды: клавиши на месте WASD при любой раскладке.
            if (keyboard[Key.A].wasPressedThisFrame)
            {
                _selected = (_selected + wheels.Length - 1) % wheels.Length;
                Highlight(true);
            }
            else if (keyboard[Key.D].wasPressedThisFrame)
            {
                _selected = (_selected + 1) % wheels.Length;
                Highlight(true);
            }
            else if (keyboard[Key.W].wasPressedThisFrame)
            {
                Turn(1);
            }
            else if (keyboard[Key.S].wasPressedThisFrame)
            {
                Turn(-1);
            }
        }

        private void Turn(int delta)
        {
            var wheel = _order[_selected];
            _digits[wheel] = (_digits[wheel] + delta + 10) % 10;

            if (IsSolved())
            {
                _openAt = Time.time + openDelay;
            }
        }

        private bool IsSolved()
        {
            if (code.Length != wheels.Length)
            {
                return false;
            }

            for (var i = 0; i < _order.Length; i++)
            {
                if (_digits[_order[i]] != code[i] - '0')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Колёса крутятся к цифре плавно: мгновенный скачок не читается как поворот.</summary>
        private void AnimateWheels()
        {
            for (var i = 0; i < wheels.Length; i++)
            {
                var target = _digits[i] * stepDegrees;

                // Ближайшим путём: с 9 на 0 колесо проходит одну цифру, а не девять назад.
                var delta = Mathf.DeltaAngle(_angles[i], target);
                _angles[i] = Mathf.MoveTowards(_angles[i], _angles[i] + delta, turnSpeed * Time.deltaTime);
                wheels[i].localRotation = _rest[i] * Quaternion.AngleAxis(_angles[i], wheelAxis);
            }
        }

        private void SortWheelsOnScreen()
        {
            var camera = Camera.main;

            if (camera == null)
            {
                return;
            }

            Array.Sort(_order, (a, b) =>
                camera.WorldToScreenPoint(wheels[a].position).x.CompareTo(camera.WorldToScreenPoint(wheels[b].position).x));
        }

        private void Highlight(bool active)
        {
            for (var i = 0; i < wheels.Length; i++)
            {
                if (_renderers[i] == null)
                {
                    continue;
                }

                var selected = active && _order[_selected] == i;
                var color = selected || !active ? _baseColors[i] : _baseColors[i] * idleBrightness;
                color.a = _baseColors[i].a;

                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _renderers[i].SetPropertyBlock(_block);
            }
        }

        // Сохранение (GDD 02.12): открытый замок помнит его флаг — после загрузки
        // замка нет, дверь открыта.
        private void Start()
        {
            if (GameSession.Current != null)
            {
                GameSession.Current.Restored += OnRestored;
            }
        }

        private void OnDestroy()
        {
            if (GameSession.Current != null)
            {
                GameSession.Current.Restored -= OnRestored;
            }
        }

        private void OnRestored()
        {
            if (string.IsNullOrWhiteSpace(openedFlag)
                || !GameSession.Current.World.TryGet<bool>(openedFlag, out var opened) || !opened)
            {
                return;
            }

            if (door != null)
            {
                door.OpenInstant();
            }

            gameObject.SetActive(false);
        }

        private void Open()
        {
            _openAt = -1f;

            if (!string.IsNullOrWhiteSpace(openedFlag) && GameSession.Current != null)
            {
                GameSession.Current.World.Set(openedFlag, true);
            }

            ShowMessage();

            if (door != null)
            {
                door.Open();
            }

            // Выключение само вернёт управление игроку: «3D просмотр» отпускает его в OnDisable.
            gameObject.SetActive(false);
        }

        /// <summary>Надпись живёт отдельно от замка: замок сейчас исчезнет вместе с детьми.</summary>
        private void ShowMessage()
        {
            if (string.IsNullOrWhiteSpace(openedText) || font == null)
            {
                return;
            }

            var canvasObject = new GameObject("LockOpenedMessage", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var labelObject = new GameObject("Text", typeof(RectTransform));
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = openedText;
            label.fontSize = 48f;
            label.color = new Color(1f, 0.85f, 0.55f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            Destroy(canvasObject, 2.5f);
        }
    }
}
