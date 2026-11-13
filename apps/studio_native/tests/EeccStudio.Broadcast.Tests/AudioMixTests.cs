using EeccStudio.Core.Audio;
using Xunit;

namespace EeccStudio.Broadcast.Tests;

public class AudioMixTests
{
    [Fact]
    public void EmptyBus_IsSilent_AndToneSurvivesTheMaster()
    {
        var engine = new AudioEngine();
        engine.SetVolume("master", 1f);
        var silent = engine.RenderMaster(1000);
        Assert.All(silent, sample => Assert.Equal(0, sample));
        Assert.True(engine.Channels.Single(channel => channel.Id == "master").CurrentLevelDb <= -60);

        var tone = new short[2000];
        for (var i = 0; i < 1000; i++)
        {
            var sample = (short)(8000 * Math.Sin(2 * Math.PI * i / 8));
            tone[i * 2] = sample;
            tone[i * 2 + 1] = sample;
        }

        engine.SubmitPcm("programme", tone);
        var mixed = engine.RenderMaster(1000);
        var energy = Energy(mixed);
        Assert.True(energy > 1000, "Le ton soumis n'est pas sorti du bus : " + energy);

        engine.SetMute("programme", true);
        engine.SubmitPcm("programme", tone);
        var muted = engine.RenderMaster(1000);
        Assert.True(Energy(muted) < 1);
    }

    private static double Energy(byte[] pcm)
    {
        double sum = 0;
        var count = pcm.Length / 2;
        for (var i = 0; i < count; i++)
        {
            var sample = BitConverter.ToInt16(pcm, i * 2);
            sum += sample * (double)sample;
        }

        return count == 0 ? 0 : Math.Sqrt(sum / count);
    }
}
