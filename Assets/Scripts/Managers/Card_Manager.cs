using UnityEngine;

public class Card_Manager : SingleTonForGameObject<Card_Manager>
{
    [SerializeField] private GameObject Character;
    //[SerializeField] private GameObject Skill;

    public void Awake()
    {
        SetInstance(this);
    }

    public void Start()
    {
        GameObject characterInstance = Instantiate(Character);
        //GameObject skillInstance = Instantiate(Skill);

        // GetComponent -> GetComponentInChildren 으로 변경!
        Canvas CharacterCanvas = characterInstance.GetComponentInChildren<Canvas>();
        //Canvas SkillCanvas = skillInstance.GetComponentInChildren<Canvas>();

        Camera targetCamera = Camera.main;

        CharacterCanvas.worldCamera = targetCamera;
        //SkillCanvas.worldCamera = targetCamera;
    }

    protected override void Dispose(bool bisDisposing)
    {

    }
}