using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            board_too_narrow,
            raised_board,
            possible_debris,
            board_too_short,
            missing_board,
            board_segmentation_error,
            missing_blocks,
            missing_chunks,
            excessive_angle,
            clearance,
            missing_wood_width_across_length,
            missing_wood_width_at_one_point, 
            blocks_protuded_from_pallet,     
            side_nails_protruding,          
            linear_support_missing_wood,       
            unsecured_horizontal_block,      
            puncture,                        
            buttedJoint,
            broken_support_board
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
            SP_V1,
            SP_V2,
            SP_V3,
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
                case DefectType.missing_wood_width_across_length: return "MWA";                     
                case DefectType.blocks_protuded_from_pallet: return "BPFP";        
                case DefectType.side_nails_protruding: return "SNP";                
                case DefectType.linear_support_missing_wood: return "LSMW";                
                case DefectType.missing_wood_width_at_one_point: return "MWAOP";         
                case DefectType.unsecured_horizontal_block: return "UHB";                
                case DefectType.puncture: return "PU";                                   
                case DefectType.buttedJoint: return "BJ";                           
                case DefectType.broken_support_board: return "BSP";                 
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
                case "LSMW": return "Maximum Linear Support Board Missing Wood in %";
                case "MWAOP": return "Missing Wood Width at One Point";    
                case "UHB": return "Unsecured Horizontal Block";         
                case "PU": return "Puncture";                                 
                case "BJ": return "Butted Joint";
                case "BSP": return "Broken Suport Board Across Width";
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
                case DefectType.missing_wood_width_across_length: return "Missing Wood Width Across Length";   
                case DefectType.blocks_protuded_from_pallet: return "Blocks Protruding From Pallet";         
                case DefectType.side_nails_protruding: return "Side Nails Protruding";                     
                case DefectType.clearance: return "Fork Clearance";                                        
                case DefectType.linear_support_missing_wood: return "Linear Support Missing Wood";              
                case DefectType.missing_wood_width_at_one_point: return "Missing Wood With at One Point";
                case DefectType.unsecured_horizontal_block: return "Unsecured Horizontal Block";         
                case DefectType.puncture: return "Puncture";
                case DefectType.buttedJoint: return "ButtedJoint";
                case DefectType.broken_support_board: return "Broken Support Board Across Width";
                default: return "?";
            }
        }

        public static string[] GetCodes()
        {
            string[] list = { "ND", "RN", "MW", "BW", "BN", "RB", "PD", "SH", "MB", "ER" ,"MO","MU","EA", "FC","MWA","RNFC","BPFP","SNP","MWMW","MWAOP","UHB","PU", "BJ" };  
            return list;
        }

        public static string[] GetPLCCodes()
        {
            string[] list = { "ND", "RN", "MW", "BW", "CK", "BN", "RB", "PD", "SH", "MB", "MO", "MU", "EA" , "FC", "MWA", "RNFC", "BPFP", "SNP","MWMW", "MWAOP","UHB","PU", "BJ" };
            return list;
        }

        public enum DefectReport
        {
            Name = 0,
            Result = 1,
            BW = 2,         // Broken Across Width
            MW = 3,         // Missing Wood
            MU = 4,         // Missing Chunks
            MWA = 5,        // Minimum Width Across Length
            RN = 6,         // Raised Nail
            SNP = 7,        // Side Nails Protruding
            BPFP = 8,       // Block Protruding from Paller
            MO = 9,         // Missing Block
            FC = 10,        // Fork Clearance
            LSMW = 11,      // Linear Support Missing Wood
            PD = 12,        // Possible Debris
            BJ = 13,         // Butted Joint
            BSP = 14      // Broken Support Board Across Width
        }
    }
}
