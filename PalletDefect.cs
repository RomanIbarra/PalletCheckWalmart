using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalletCheck
{
    public class PalletDefect
    {
        public enum DefectType
        {
            none,
            raised_nail,
            missing_wood,
            broken_across_width,
            //too_many_cracks,
            board_too_narrow,
            raised_board,
            possible_debris,
            board_too_short,
            missing_board,
            board_segmentation_error,
            missing_blocks,  //Added by Jack
            missing_chunks, //Added by Jack
            excessive_angle, //Added by Jack
            clearance,
        }

        public enum DefectLocation
        {
            Pallet,
            B_V1,
            B_V2,
            B_V3,
            B_H1,
            B_H2,
            T_H1,
            T_H2,
            T_H3,
            T_H4,
            T_H5,
            T_H6,
            T_H7,
            L_B1,
            L_B2,
            L_B3,
            R_B1,
            R_B2,
            R_B3,
            // For Left and Right //Added by Jack
        }

        public DefectType Type { get; set; }
        public DefectLocation Location { get; set; }
        public string Comment { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }

        public double MarkerX1 { get; set; }
        public double MarkerY1 { get; set; }
        public double MarkerX2 { get; set; }
        public double MarkerY2 { get; set; }
        public double MarkerRadius { get; set; }
        public string MarkerTag { get; set; }

        public PalletDefect(DefectLocation loc, DefectType type, string Comment)
        {
            this.Type = type;
            this.Location = loc;
            this.Comment = Comment;
            this.Name = TypeToName(type);
            this.Code = TypeToCode(type);
        }


        public void SetRectMarker(double X1, double Y1, double X2, double Y2, string Tag)
        {
            MarkerX1 = X1;
            MarkerY1 = Y1;
            MarkerX2 = X2;
            MarkerY2 = Y2;
            MarkerTag = Tag;
        }

        public void SetCircleMarker(double X, double Y, double R, string Tag)
        {
            MarkerX1 = X;
            MarkerY1 = Y;
            MarkerRadius = R;
            MarkerTag = Tag;
        }







        //public class Marker
        //{
        //    public double X;
        //    public double Y;
        //    public double Radius;
        //    public double Width;
        //    public double Height;
        //    public string Tag;
        //};
        //public List<Marker> Markers = new List<Marker>();

        //public void AddRectMarker(double X, double Y, double W, double H, string Tag)
        //{
        //    Marker M = new Marker();
        //    M.X = X;
        //    M.Y = Y;
        //    M.Radius = 0;
        //    M.Width = W;
        //    M.Height = H;
        //    M.Tag = Tag;
        //    Markers.Add(M);
        //}
        //public void AddCircleMarker(double X, double Y, double R, string Tag)
        //{
        //    Marker M = new Marker();
        //    M.X = X;
        //    M.Y = Y;
        //    M.Radius = R;
        //    M.Width = R * 2;
        //    M.Height = R * 2;
        //    M.Tag = Tag;
        //    Markers.Add(M);
        //}






        public static string TypeToCode(DefectType type)
        {
            switch (type)
            {
                case DefectType.none: return "ND";
                case DefectType.raised_nail: return "RN";
                case DefectType.missing_wood: return "MW";
                case DefectType.broken_across_width: return "BW";
                //case DefectType.too_many_cracks: return "CK";
                case DefectType.board_too_narrow: return "BN";
                case DefectType.raised_board: return "RB";
                case DefectType.possible_debris: return "PD";
                case DefectType.board_too_short: return "SH";
                case DefectType.missing_board: return "MB";
                case DefectType.board_segmentation_error: return "ER";
                case DefectType.missing_blocks:return "MO";  //Added by Jack
                case DefectType.missing_chunks:return "MU";
                case DefectType.excessive_angle:return "EA";
                case DefectType.clearance: return "FC";
                default: return "?";
            }
        }

        public static string CodeToName(string code)
        {
            switch (code)
            {
                case "ND": return "None";
                case "RN": return "Raised Nail";
                case "MW": return "Missing Wood";
                case "BW": return "Broken Across Width";
                case "BN": return "Board Too Narrow";
                case "RB": return "Raised Board";
                case "PD": return "Possible Debris";
                case "SH": return "Board Too Short";
                case "MB": return "Missing Board";
                case "ER": return "Segmentation Error";
                case "MO": return "Missing Blocks";
                case "MU": return "Missing Chunks";
                case "EA": return "Excessive Angle";
                case "FC": return "Fork Clearance";
                default: return "?";
            }
        }

        public static string TypeToName(DefectType type)
        {
            switch (type)
            {
                case DefectType.none: return "None";
                case DefectType.raised_nail: return "Raised Nail";
                case DefectType.missing_wood: return "Missing Wood";
                case DefectType.broken_across_width: return "Broken Across Width";
                //case DefectType.too_many_cracks: return "Too Many Cracks";
                case DefectType.board_too_narrow: return "Board Too Narrow";
                case DefectType.raised_board: return "Raised Board";
                case DefectType.possible_debris: return "Possible Debris";
                case DefectType.board_too_short: return "Board Too Short";
                case DefectType.missing_board: return "Missing Board";
                case DefectType.board_segmentation_error: return "Segmentation Error";
                case DefectType.missing_blocks: return "Missing Blocks";  //Jack Added
                case DefectType.missing_chunks: return "Missing Chunks";  //Jack Added
                case DefectType.excessive_angle: return "Excessive Angle"; //Jack Added
                default: return "?";
            }
        }

        public static string[] GetCodes()
        {
            string[] list = { "ND", "RN", "MW", "BW", "BN", "RB", "PD", "SH", "MB", "ER" ,"MO","MU","EA", "FC" };  //Jack added the last 3
            return list;
        }

        public static string[] GetPLCCodes()
        {
            string[] list = { "ND", "RN", "MW", "BW", "CK", "BN", "RB", "PD", "SH", "MB", "MO", "MU", "EA" , "FC" };//Jack added the last 3
            return list;
        }
    }
}
