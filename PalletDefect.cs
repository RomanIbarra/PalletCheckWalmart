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
            raised_nail_fastener_cutoff,     //added by HUB
            missing_wood_width_across_length,//added by HUB
            blocks_protuded_from_pallet,     //added by HUB
            side_nails_protruding,           //added by HUB 
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
                case DefectType.missing_wood_width_across_length: return "MWA";       //Added by HUB 
                case DefectType.raised_nail_fastener_cutoff: return "RNFC";          //Added by HUB
                case DefectType.blocks_protuded_from_pallet: return "BPFP";         //Added by HUB 
                case DefectType.side_nails_protruding: return "SNP";               //Added by HUB 
                 
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
                case "MWA": return "Missing Wood Width Across Length";   //Added by HUB 
                case "RNFC": return "Raised Nail Fastener Cutoff";      //Added by HUB 
                case "BPFP": return "Blocks Protruding From Pallet";   //Added by HUB 
                case "SNP":  return "Side Nails Protruding";          //Added by HUB 
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
                case DefectType.missing_wood_width_across_length: return "Missing Wood Width Across Length";  //Added by HUB 
                case DefectType.raised_nail_fastener_cutoff: return "Raised Nail Fastener Cutoff";           //Added by HUB
                case DefectType.blocks_protuded_from_pallet: return "Blocks Protruding From Pallet";        //Added by HUB 
                case DefectType.side_nails_protruding: return "Side Nails Protruding";                     //Added by HUB
                case DefectType.clearance: return "Fork Clearance";
                default: return "?";
            }
        }

        public static string[] GetCodes()
        {
            string[] list = { "ND", "RN", "MW", "BW", "BN", "RB", "PD", "SH", "MB", "ER" ,"MO","MU","EA", "FC","MWA","RNFC","BPFP","SNP" };  //Last 4 added by HUB 
            return list;
        }

        public static string[] GetPLCCodes()
        {
            string[] list = { "ND", "RN", "MW", "BW", "CK", "BN", "RB", "PD", "SH", "MB", "MO", "MU", "EA" , "FC", "MWA", "RNFC", "BPFP", "SNP" };//Last 4 added by HUB
            return list;
        }

        public enum DefectsCSV
        {
            T_RN = 1,       // Top Raised Nail   
            T_MB = 2,       // Top Missing Board
            T_BW = 3,       // Top Broken Across Width
            T_MW = 4,       // Top Missing Wood
            B_RN = 5,       // Bottom Raised Nail
            B_MB = 6,       // Bottom Missing Board
            B_BW = 7,       // Bottom Crack across width
            B_MW = 8,       // Bottom Missing Wood
            L_MO = 9,       // Left Missing Block
            L_EA = 10,      // Left Excesive Angle (Rotated Block)
            L_MU = 11,      // Left Missing Chunk
            R_MO = 12,      // Right Missing Block
            R_EA = 13,      // Right Excesive Angle (Rotated Block)
            R_MU = 14,      // Right Missing Chunk
            R_FC = 15,      // Right Fork Clearance
            L_SNP = 16,     // Left Side Nails Protruding
            L_BPFP = 17,    // Left Block Protruding from Pallet
            R_SNP = 18,     // Right Side Nails Protruding From Pallet
            R_BPFP = 19,    // Right Block Protuded From Pallet
            T_MWA = 20      // Top Missing Wood Across the Length
        }

        public enum DefectsCSV_Phase1A
        {
            T_MWA = 1,      // Top Missing Wood Across Lenght
            T_RNFC = 2,     // Top Raised Nail Fastener Cut off
            B_RNFC = 3,     // Bottom Raised Nail Fastener Cut off
            R_FC = 4,       // Right Fork Clearance
            R_BPFP = 5,     // Right Block Protruding From Pallet
            R_SNP = 6,      // Right Side Nail Protruding
            L_FC = 7,       // Left Fork Clearance
            L_BPFP = 8,     // Left Block Protruding From Pallet
            L_SNP = 9       // Left Side Nail Protruding
        }

    }
}
