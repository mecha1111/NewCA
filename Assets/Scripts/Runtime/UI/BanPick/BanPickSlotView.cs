using CrossAccel.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>PickScene 하단 픽 슬롯 1칸 (SPEC 4번: 빈칸(점선) / 채워지면 초상화+이름).</summary>
    public class BanPickSlotView : MonoBehaviour
    {
        private GameObject _emptyState;
        private GameObject _filledState;
        private Text _nameText;

        private void Awake()
        {
            _emptyState = transform.Find("EmptyState")?.gameObject;
            _filledState = transform.Find("FilledState")?.gameObject;
            _nameText = transform.Find("FilledState/NameText")?.GetComponent<Text>();
        }

        public void SetEmpty()
        {
            if (_emptyState != null) _emptyState.SetActive(true);
            if (_filledState != null) _filledState.SetActive(false);
        }

        public void SetFilled(CharacterData data)
        {
            if (_emptyState != null) _emptyState.SetActive(false);
            if (_filledState != null) _filledState.SetActive(true);
            if (_nameText != null) _nameText.text = data.Name;
        }
    }
}
