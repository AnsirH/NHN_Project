using NUnit.Framework;
using OutGame.Logic.Audio;
using UnityEngine;

namespace OutGame.Tests.EditMode
{
    public class SoundSettingsTests
    {
        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(SoundSettings.VolumeKey);

        [Test]
        public void MasterVolume_NoSavedValue_ReturnsDefault()
        {
            Assert.AreEqual(SoundSettings.DefaultVolume, SoundSettings.MasterVolume);
        }

        [Test]
        public void MasterVolume_SetThenGet_ReturnsSetValue()
        {
            SoundSettings.MasterVolume = 0.4f;

            Assert.AreEqual(0.4f, SoundSettings.MasterVolume, 0.0001f);
        }

        [Test]
        public void MasterVolume_SetAboveOne_ClampsToOne()
        {
            SoundSettings.MasterVolume = 1.5f;

            Assert.AreEqual(1f, SoundSettings.MasterVolume);
        }

        [Test]
        public void MasterVolume_SetBelowZero_ClampsToZero()
        {
            SoundSettings.MasterVolume = -0.5f;

            Assert.AreEqual(0f, SoundSettings.MasterVolume);
        }
    }
}
