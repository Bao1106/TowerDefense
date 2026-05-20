using System;
using UnityEngine;

namespace Services
{
   public interface IAudioService
   {
      public enum AudioType
      {
         Walk,
         Run,
         Jump,
         Shoot,
         Die,
      }
       
      AudioSource AudioSource { get; set; }
      AudioClip GetSound(AudioType audioType);
      void PlaySound(AudioType audioType);
      void StopSound(AudioType audioType);

      public class AudioService : IAudioService
      {
         public AudioSource AudioSource { get; set; }

         public AudioClip GetSound(AudioType audioType)
         {
            var audioClip = Resources.Load<AudioClip>($"Sound/{audioType}");
            if (audioClip != null) 
               return audioClip;

            throw new ArgumentException($"Sound type {audioType} not found");
         }

         public void PlaySound(AudioType audioType)
         {
            if (AudioSource == null) {
               throw new ArgumentException($"Audio Source is null");
            }

            AudioSource.clip = GetSound(audioType);
            AudioSource.Play();
         }

         public void StopSound(AudioType audioType)
         {
            
         }
      }
   }

   public interface IPrefabService
   {
      
   }
}
