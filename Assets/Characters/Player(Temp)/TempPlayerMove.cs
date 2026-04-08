using UnityEngine;

public class TempPlayerMove : MonoBehaviour
{
    Animator animator;
    float speed = 0.0f;
    public float acceleration = 0.1f;
    public float deacceleration = 0.5f;
    int velocityHash;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();

        velocityHash = Animator.StringToHash("Speed");
    }

    // Update is called once per frame
    void Update()
    {
        //get key input from Player
        bool forwardPressed = Input.GetKey("w");
        bool runPressed = Input.GetKey(KeyCode.LeftShift);

        if (forwardPressed && speed < 1.0f)
        {
            speed += Time.deltaTime * acceleration;
        }

        if (!forwardPressed && speed > 0.0f)
        {
            speed -= Time.deltaTime * deacceleration;
        }

        if (!forwardPressed && speed < 0.0f)
        {
            speed = 0.0f;
        }


        animator.SetFloat(velocityHash, speed);
    }
}