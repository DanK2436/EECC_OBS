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

    [Fact]
    public void Balance_a_gauche_laisse_le_canal_droit_silencieux()
    {
        var engine = new AudioEngine();
        engine.SetVolume("master", 1f);
        engine.GetOrCreateDsp("programme").EnableHighPass = false;
        engine.GetOrCreateDsp("programme").EnableNoiseGate = false;
        engine.GetOrCreateDsp("programme").EnableCompressor = false;
        engine.GetOrCreateDsp("programme").EnableLimiter = false;
        engine.SetBalance("programme", -1f);
        var tone = new short[400];
        for (var i = 0; i < 200; i++)
        {
            tone[i * 2] = 8000;
            tone[i * 2 + 1] = 8000;
        }

        engine.SubmitPcm("programme", tone);
        var mixed = engine.RenderMaster(200);
        double right = 0;
        var pairs = mixed.Length / 4;
        for (var i = 0; i < pairs; i++)
            right += Math.Abs(BitConverter.ToInt16(mixed, i * 4 + 2));
        Assert.True(right < 1);
    }

    [Fact]
    public void Ecoute_seule_retire_la_piste_du_programme()
    {
        var engine = new AudioEngine();
        engine.SetVolume("master", 1f);
        var dsp = engine.GetOrCreateDsp("choir_music");
        dsp.EnableHighPass = false;
        dsp.EnableNoiseGate = false;
        dsp.EnableCompressor = false;
        dsp.EnableLimiter = false;
        engine.SetMonitor("choir_music", AudioMonitorMode.MonitorOnly);
        var tone = new short[400];
        for (var i = 0; i < tone.Length; i++)
            tone[i] = 6000;
        engine.SubmitPcm("choir_music", tone);
        var mixed = engine.RenderMaster(200);
        Assert.True(Energy(mixed) < 1);
        Assert.True(engine.Channels.Single(channel => channel.Id == "choir_music").CurrentLevelDb > -40);
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
