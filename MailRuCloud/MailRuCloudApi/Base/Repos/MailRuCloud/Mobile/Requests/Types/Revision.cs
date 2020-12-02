﻿using System;

namespace YaR.Clouds.Base.Repos.MailRuCloud.Mobile.Requests.Types
{
    class Revision
    {
        private readonly ulong _newBgn;
        private readonly TreeId _newTreeId;
        private readonly TreeId _treeId;
        private readonly ulong _bgn;

        private Revision()
        {
        }

        private Revision(TreeId treeId, ulong bgn) : this()
        {
            _treeId = treeId;
            _bgn = bgn;
        }

        private Revision(TreeId treeId, ulong bgn, TreeId newTreeId) : this(treeId, bgn)
        {
            _newTreeId = newTreeId;
        }

        private Revision(TreeId treeId, ulong bgn, TreeId newTreeId, ulong newBgn) : this(treeId, bgn, newTreeId)
        {
            _newBgn = newBgn;
        }


        public static Revision FromStream(ResponseBodyStream stream)
        {
            short ver = stream.ReadShort();
            return ver switch
            {
                0 => new Revision(),
                1 => new Revision(TreeId.FromStream(stream), stream.ReadULong()),
                2 => new Revision(TreeId.FromStream(stream), stream.ReadULong()),
                3 => new Revision(TreeId.FromStream(stream), stream.ReadULong(), TreeId.FromStream(stream),
                    stream.ReadULong()),
                4 => new Revision(TreeId.FromStream(stream), stream.ReadULong(), TreeId.FromStream(stream),
                    stream.ReadULong()),
                5 => new Revision(TreeId.FromStream(stream), stream.ReadULong(), TreeId.FromStream(stream)),
                _ => throw new Exception("Unknown revision " + ver)
            };
        }
    }
}