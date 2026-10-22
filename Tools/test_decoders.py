import unittest
from analyze_rom import zero_run,mask_blocks
class DecoderTests(unittest.TestCase):
    def test_zero_escape(self):
        d,p,s=zero_run(bytes([0,255,254]),0,0)
        self.assertEqual(d,bytes([255])+bytes(31));self.assertEqual(p,3);self.assertEqual(s,[32])
    def test_zero_overshoot(self):self.assertEqual(zero_run(bytes([1,255]),0,0)[2],[33])
    def test_zero_multiple(self):self.assertEqual(zero_run(bytes([255,255]),0,1)[0],bytes(64))
    def test_mask_full(self):self.assertEqual(mask_blocks(bytes([1,170,255,255,255,255,255]),0)[0],bytes([170])*32)
    def test_mask_literal(self):self.assertEqual(mask_blocks(bytes([0])+bytes(range(32))+bytes([255]),0)[0],bytes(range(32)))
    def test_mask_overlap(self):
        b=bytes([2,170,128,0,0,0,187,192,0,0,0])+bytes(range(30))+bytes([255])
        self.assertEqual(mask_blocks(b,0)[0],bytes([187,187])+bytes(range(30)))
    def test_mask_truncated(self):
        with self.assertRaises(IndexError):mask_blocks(bytes([0,1]),0)
    def test_limit(self):
        with self.assertRaises(ValueError):mask_blocks(bytes(66),0,1)
if __name__=='__main__':unittest.main()
