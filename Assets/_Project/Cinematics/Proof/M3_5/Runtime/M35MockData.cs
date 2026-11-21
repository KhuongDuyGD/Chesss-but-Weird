#if UNITY_EDITOR || (DEVELOPMENT_BUILD && ROYAL_ASCENSION_DEMO)
using System;
using UnityEngine;

namespace ChessButWeird.CinematicProof.M35
{
    // Presentation-only enum and fixtures. No dependency on reward services or economy.
    public enum MockRarity { Common, Rare, Epic, Legendary }
    public enum MockArtwork { KingSketch, KnightSketch, Missing }
    [Serializable]
    public sealed class MockReward
    {
        public string id, name;
        public MockRarity rarity;
        public MockArtwork artwork;
        public bool duplicate;
        public MockReward(string id,string name,MockRarity rarity,MockArtwork artwork,bool duplicate=false)
        {this.id=id;this.name=name;this.rarity=rarity;this.artwork=artwork;this.duplicate=duplicate;}
        public MockReward Copy()=>new MockReward(id,name,rarity,artwork,duplicate);
    }
    public static class M35MockFixtures
    {
        public static MockReward Single(MockRarity rarity)=>new MockReward("mock-"+rarity,
            new[]{"Paperbound Squire","The Inkstride Knight","Bishop of the Unwritten Stars","The Crown of a Thousand Unwritten Kingdoms"}[(int)rarity],
            rarity,rarity==MockRarity.Rare?MockArtwork.KnightSketch:MockArtwork.KingSketch);
        public static MockReward[] Ten()=>new[]{
            Single(MockRarity.Common),Single(MockRarity.Rare),Single(MockRarity.Epic),Single(MockRarity.Legendary),
            new MockReward("mock-Common","Paperbound Squire",MockRarity.Common,MockArtwork.KingSketch,true),
            new MockReward("mock-missing","An Unfinished Royal Sketch",MockRarity.Rare,MockArtwork.Missing),
            new MockReward("mock-long","The Wandering Bishop Who Guards Every Forgotten Margin of the Everlasting Royal Sketchbook",MockRarity.Epic,MockArtwork.KingSketch),
            new MockReward("mock-Legendary","The Crown of a Thousand Unwritten Kingdoms",MockRarity.Legendary,MockArtwork.KingSketch,true),
            new MockReward("mock-rook","Rampart of Quiet Pages",MockRarity.Rare,MockArtwork.KingSketch),
            new MockReward("mock-Common","Paperbound Squire",MockRarity.Common,MockArtwork.KingSketch,true)};
    }
}


#endif
