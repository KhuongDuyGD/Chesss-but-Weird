using System.Collections.Generic;

namespace ChessButWeird.Domain
{
    /// <summary>Read-only blast projection, including a second armed original Queen.
    /// The supplied board already contains the moved capturer or removed ranged target.</summary>
    public readonly struct ExplosionChainBoardView<TBoard> : IReadOnlyBoard where TBoard:IReadOnlyBoard
    {
        private readonly TBoard board;
        private readonly ulong removed;
        public ExplosionChainBoardView(TBoard board,Square initialCenter,IBuffContextProvider buffs)
        {
            this.board=board;ulong mask=0,visited=0;var centers=new Queue<Square>();centers.Enqueue(initialCenter);
            while(centers.Count>0)
            {
                var center=centers.Dequeue();if(!center.IsValid)continue;
                ulong centerBit=1UL<<(center.Rank*8+center.File);if((visited&centerBit)!=0)continue;visited|=centerBit;
                for(int x=center.File-1;x<=center.File+1;x++)for(int y=center.Rank-1;y<=center.Rank+1;y++)
                {
                    var square=new Square(x,y);if(!square.IsValid)continue;
                    ulong bit=1UL<<(y*8+x);if((mask&bit)!=0)continue;var piece=board.GetPiece(square);
                    if(piece.IsEmpty||piece.Kind==PieceKind.King)continue;
                    mask|=bit;if(buffs.GetContext(piece).BomberArmed)centers.Enqueue(square);
                }
            }
            removed=mask;
        }
        public PieceState GetPiece(Square square)=>!square.IsValid||(removed&(1UL<<(square.Rank*8+square.File)))!=0?default:board.GetPiece(square);
    }
}
