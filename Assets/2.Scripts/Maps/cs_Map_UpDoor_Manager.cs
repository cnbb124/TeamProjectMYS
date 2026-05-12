using System.Collections;
using UnityEngine;

public class cs_Map_UpDoor_Manager : MonoBehaviour
{
    private AudioSource audioSource;
    public Transform door;
    public float openDistance = 4f;
    public float speed = 2f;
    public float closeDelay = 2f;

    private Vector3 closedPos;
    private Vector3 openPos;
    private bool isOpen = false;
    private Coroutine closeCoroutine;

    void Start()
    {
        closedPos = door.localPosition;
        openPos = closedPos + new Vector3(0, openDistance, 0);

        audioSource = GetComponentInChildren<AudioSource>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (closeCoroutine != null) StopCoroutine(closeCoroutine);
            isOpen = true;
            audioSource.Play();
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
            door.localPosition = Vector3.Lerp(door.localPosition, openPos, speed * Time.deltaTime);
        }
        else
        {
            door.localPosition = Vector3.Lerp(door.localPosition, closedPos, speed * Time.deltaTime);
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
