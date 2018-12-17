using UnityEngine;
using System.Collections;

public class Obstacle : MonoBehaviour
{
   private Animation ani;
   public AnimationClip clip_die, clip_idle;
   // Use this for initialization
   void Start()
   {
      ani = GetComponent<Animation>();

      PlayAnimation("idle");
   }

   // Update is called once per frame
   void Update()
   {
   }

   public void PlayAnimation(string aniName, bool loop = true)
   {
      ani.Stop();
      AnimationClip temp = aniName == "die" ? clip_die : clip_idle;
      ani.AddClip(temp, "clip");
      ani.Play("clip", PlayMode.StopAll);
      ani.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
   }
}
