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
            missing_wood_width_across_length,//added by HUB
            blocks_protuded_from_pallet,     //added by HUB
            side_nails_protruding,           //added by HUB 
            middle_board_missing_wood,            //added by HUB
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
            T_H8,
            T_H9,
            L_B1,
            L_B2,
            L_B3,
            R_B1,
            R_B2,
            R_B3,
            F_B1,
            F_B2,
            F_B3,
            BK_B1,
            BK_B2,
            BK_B3,
            R_MB,
            L_MB,
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
                case DefectType.board_too_narrow: return "BN";
                case DefectType.raised_board: return "RB";
                case DefectType.possible_debris: return "PD";
                case DefectType.board_too_short: return "SH";
                case DefectType.missing_board: return "MB";
                case DefectType.board_segmentation_error: return "ER";
                case DefectType.missing_blocks:return "MO";
                case DefectType.missing_chunks:return "MU";
                case DefectType.excessive_angle:return "EA";
                case DefectType.clearance: return "FC";
                case DefectType.missing_wood_width_across_length: return "MWA";       //Added by HUB               
                case DefectType.blocks_protuded_from_pallet: return "BPFP";         //Added by HUB 
                case DefectType.side_nails_protruding: return "SNP";               //Added by HUB 
                case DefectType.middle_board_missing_wood: return "MWMW";               //Added by HUB 

                default: return "?";
            }
        }

        public static string CodeToName(string code)
        {
            switch (code)
            {
                case "ND": return "None";
                case "RN": return "Raised Nail";
                case "MW": return "Maximum Missing Wood in %";
                case "BW": return "Broken Across Width";
                case "BN": return "Board Too Narrow";
                case "RB": return "Raised Board";
                case "PD": return "Possible Debris";
                case "SH": return "Board Too Short";
                case "MB": return "Missing Board";
                case "ER": return "Segmentation Error";
                case "MO": return "Maximum Missing Blocks in %";
                case "MU": return "Maximum Missing Chunks";
                case "EA": return "Excessive Angle";
                case "FC": return "Minimum Fork Clearance in %";
                case "MWA": return "Minimum Width Across Length";          
                case "BPFP": return "Blocks Protruding From Pallet";    
                case "SNP": return "Maximum Nails Protruding from Side";          
                case "MBMW": return "Maximum Linear Support Board Missing Wood in %";
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
                case DefectType.board_too_narrow: return "Board Too Narrow";
                case DefectType.raised_board: return "Raised Board";
                case DefectType.possible_debris: return "Possible Debris";
                case DefectType.board_too_short: return "Board Too Short";
                case DefectType.missing_board: return "Missing Board";
                case DefectType.board_segmentation_error: return "Segmentation Error";
                case DefectType.missing_blocks: return "Missing Blocks";
                case DefectType.missing_chunks: return "Missing Chunks";
                case DefectType.excessive_angle: return "Excessive Angle";
                case DefectType.missing_wood_width_across_length: return "Missing Wood Width Across Length";  //Added by HUB 
                case DefectType.blocks_protuded_from_pallet: return "Blocks Protruding From Pallet";        //Added by HUB 
                case DefectType.side_nails_protruding: return "Side Nails Protruding";                     //Added by HUB
                case DefectType.clearance: return "Fork Clearance";
                case DefectType.middle_board_missing_wood: return "Middle Board Missing Wood";
                default: return "?";
            }
        }

        public static string[] GetCodes()
        {
            string[] list = { "ND", "RN", "MW", "BW", "BN", "RB", "PD", "SH", "MB", "ER" ,"MO","MU","EA", "FC","MWA","RNFC","BPFP","SNP","MWMW" };  //Last added by HUB 
            return list;
        }

        public static string[] GetPLCCodes()
        {
            string[] list = { "ND", "RN", "MW", "BW", "CK", "BN", "RB", "PD", "SH", "MB", "MO", "MU", "EA" , "FC", "MWA", "RNFC", "BPFP", "SNP","MWMW" };//Last 4 added by HUB
            return list;
        }

        public enum DefectsCSV_Phase1A
        {
            T_MWA = 1,      // Top Missing Wood Across Lenght
            T_RN = 2,       // Top Raised Nail Fastener Cut off
            B_RN = 3,       // Bottom Raised Nail Fastener Cut off
            R_FC = 4,       // Right Fork Clearance
            R_BPFP = 5,     // Right Block Protruding From Pallet
            R_RN = 6,       // Right Side Nail Protruding
            L_FC = 7,       // Left Fork Clearance
            L_BPFP = 8,     // Left Block Protruding From Pallet
            L_RN = 9,       // Left Side Nail Protruding
            F_RN = 10,      // Front Nail Protruding
            F_MO= 11,       // Front Missing Block
            BK_RN = 12,     // Back Nail Protruding
            BK_MO = 13,     // Back Missing Block
            R_MB = 14,      // Right Middle Board Missing Wood
            L_MB = 15       // Left Middle Board Missing Wood
        }

        public enum DefectReport
        {
            Name = 0,
            Result = 1,
            BW = 2,
            MW = 3,
            MU = 4,
            MWA = 5,    
            RN = 6,
            SNP = 7,
            BPFP = 8,
            MO = 9,
            FC = 10,        
            MBMW = 11,
            PD = 12
        }
    }
}
