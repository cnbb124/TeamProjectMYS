using System.Collections;
using UnityEngine;

public class cs_Map_SlideDoor_Manager : MonoBehaviour
{
    [Header("¹® ¿©´ÝÈû ¼Ò¸®")]
    private AudioSource audioSource;
    [Header("¹® ¼³Á¤")]
    public Transform[] door;
    public float openDistance = 2f;
    public float speed = 2f;
    public float closeDelay = 2f;

    private Vector3 closedPos_L;
    private Vector3 closedPos_R;
    private Vector3 openPos_L;
    private Vector3 openPos_R;
    private bool isOpen = false;
    private Coroutine closeCoroutine;

    void Start()
    {
        closedPos_L = door[0].localPosition;
        openPos_L = closedPos_L + new Vector3(0, 0, openDistance);
        closedPos_R = door[1].localPosition;
        openPos_R = closedPos_R + new Vector3(0, 0, -openDistance);

        //audioSource = GetComponentInChildren<AudioSource>();
        audioSource = GetComponent<AudioSource>();
    }

    void OnTriggerEnter(Collider other)
    {
        
        if (other.CompareTag("Player"))
        {
            if (!isOpen)
            {
                audioSource.Play();
            }
            if (closeCoroutine != null) StopCoroutine(closeCoroutine);
            isOpen = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            closeCoroutine = StartCoroutine(CloseAfterDelay());
        }
    }

    void Update()
    {
        if (isOpen)
        {
            door[0].localPosition = Vector3.Lerp(door[0].localPosition, openPos_L, speed * Time.deltaTime);
            door[1].localPosition = Vector3.Lerp(door[1].localPosition, openPos_R, speed * Time.deltaTime);
        }
        else
        {
            door[0].localPosition = Vector3.Lerp(door[0].localPosition, closedPos_L, speed * Time.deltaTime);
            door[1].localPosition = Vector3.Lerp(door[1].localPosition, closedPos_R, speed * Time.deltaTime);
        }
    }

    IEnumerator CloseAfterDelay()
    {
        yield return new WaitForSeconds(closeDelay);
        isOpen = false;
        yield return new WaitForSeconds(0.1f);
        audioSource.Play();
    }
}
